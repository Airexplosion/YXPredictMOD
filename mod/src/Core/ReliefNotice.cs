namespace YxPrediction
{
    // A notice starts its three seconds only once the view can actually show it.
    public sealed class ReliefNotice
    {
        public const double Duration = 3;
        public long PendingAmount { get; private set; }
        public long Amount { get; private set; }
        public bool Visible { get; private set; }
        double _hideAt;

        public void Notify(long amount)
        {
            if (amount > 0) PendingAmount += amount;
        }
        public void ShowPending(double now)
        {
            if (PendingAmount <= 0) return;
            Amount = (Visible ? Amount : 0) + PendingAmount;
            PendingAmount = 0;
            Visible = true;
            _hideAt = now + Duration;
        }
        public void Update(double now)
        {
            if (Visible && now >= _hideAt) Dismiss();
        }
        public void Dismiss() { Visible = false; Amount = 0; }
    }
}
