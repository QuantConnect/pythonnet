namespace Algorithm
{
    public class Algorithm
    {
        public SecurityPortfolioManager Portfolio { get; }

        public Algorithm() => Portfolio = new SecurityPortfolioManager();

        public virtual void Initialize() { }

        public virtual void OnData(string data) { }

        public void SetCash(double cash) => Portfolio.SetCash(cash);

        public override string ToString() => $"Algorithm.Portfolio.Cash: {Portfolio.Cash}";
    }

    public class SecurityPortfolioManager
    {
        public double Cash { get; private set; }

        public void SetCash(double cash) => Cash = cash;
    }
}
