using YxPrediction;

internal static partial class Program
{
    static void ReliefNoticeChecks()
    {
        Check("relief popup starts only after an actual queued grant is presented", delegate
        {
            var notice = new ReliefNotice(); notice.Update(100); True(!notice.Visible);
            notice.Notify(0); notice.Notify(-1); Equal(notice.PendingAmount, 0L);
            notice.Notify(50); notice.Update(1000); True(!notice.Visible); Equal(notice.PendingAmount, 50L);
            notice.ShowPending(1000); True(notice.Visible); Equal(notice.Amount, 50L); Equal(notice.PendingAmount, 0L);
        });
        Check("relief popup expires at exactly three seconds and repeated updates cannot reopen it", delegate
        {
            var notice = new ReliefNotice(); notice.Notify(50); notice.ShowPending(10);
            notice.Update(12.999); True(notice.Visible); notice.Update(13); True(!notice.Visible);
            notice.ShowPending(14); notice.Update(100); True(!notice.Visible); Equal(notice.Amount, 0L);
        });
        Check("confirm dismisses early while a later actual grant gets its full three seconds", delegate
        {
            var notice = new ReliefNotice(); notice.Notify(50); notice.ShowPending(10);
            notice.Dismiss(); notice.Update(10.5); True(!notice.Visible); Equal(notice.PendingAmount, 0L);
            notice.Notify(50); notice.ShowPending(11); notice.Update(13); True(notice.Visible);
            notice.Update(14); True(!notice.Visible);
        });
        Check("new grants aggregate without being lost and extend visibility from the new grant", delegate
        {
            var notice = new ReliefNotice(); notice.Notify(50); notice.Notify(50); notice.ShowPending(0);
            Equal(notice.Amount, 100L); notice.Update(2); notice.Notify(50); notice.ShowPending(2);
            Equal(notice.Amount, 150L); notice.Update(4.999); True(notice.Visible); notice.Update(5); True(!notice.Visible);
        });
    }
}
