using Vela.Windows.Services;
using Windows.ApplicationModel.DataTransfer;

internal static class ShelfChecks
{
    private static void Require(bool value,string message)
    { if(!value) throw new InvalidOperationException(message); }

    internal static async Task Cancellation()
    {
        var shelf=new ShelfService();
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var data=new DataPackage();
        data.SetDataProvider(StandardDataFormats.Text,request=>
        {
            var deferral=request.GetDeferral(); ready.SetResult();
            _=Complete();
            async Task Complete()
            {
                try { await release.Task; request.SetData("late fixture"); }
                finally { deferral.Complete(); finished.SetResult(); }
            }
        });
        var pending=shelf.Add(data.GetView());
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            shelf.CancelPendingAdds(); release.TrySetResult();
            bool canceled=false;
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); } catch(OperationCanceledException) { canceled=true; }
            Require(canceled && shelf.Items.Count==0,"Disabled shelf accepted an in-flight provider.");
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var next=new DataPackage(); next.SetText("new session");
            Require(await shelf.Add(next.GetView())==1 && shelf.Items.Single().Text=="new session","Cancellation poisoned later additions.");
            using var canceledToken=new CancellationTokenSource(); canceledToken.Cancel();
            canceled=false;
            try { await shelf.Add(next.GetView(),canceledToken.Token); } catch(OperationCanceledException) { canceled=true; }
            Require(canceled && shelf.Items.Count==1,"A closed tool accepted a canceled addition.");
        }
        finally { release.TrySetResult(); }
    }

    internal static async Task DragIdentity()
    {
        var shelf=new ShelfService(); var data=new DataPackage(); data.SetText("same fixture");
        await shelf.Add(data.GetView()); var original=shelf.Items.Single();
        shelf.Items.Remove(original); await shelf.Add(data.GetView()); var replacement=shelf.Items.Single();
        shelf.CompleteDrag([original],DataPackageOperation.Copy,true);
        Require(shelf.Items.Count==1 && ReferenceEquals(shelf.Items[0],replacement),"Late drag completion removed a newly added identical entry.");
        foreach(var result in new[]{DataPackageOperation.None,DataPackageOperation.Move,DataPackageOperation.Link,DataPackageOperation.Copy|DataPackageOperation.Move})
            shelf.CompleteDrag([replacement],result,true);
        Require(shelf.Items.Count==1,"An unoffered transfer operation removed a shelf entry.");
        shelf.CompleteDrag([replacement],DataPackageOperation.Copy,false);
        Require(shelf.Items.Count==1,"Disabled removal discarded a shelf entry.");
        shelf.CompleteDrag([replacement],DataPackageOperation.Copy,true);
        Require(shelf.Items.Count==0,"An accepted copy did not remove its exact entry.");
    }
}
