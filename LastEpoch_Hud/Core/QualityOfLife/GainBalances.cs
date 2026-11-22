using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.QualityOfLife;

// Reconciles native gain events with wallet samples without crediting the same gain twice.
public sealed class GainBalances
{
    readonly SessionGains session;
    readonly Dictionary<int, (long Owner, GainCurrency Currency, long Balance)> balances = new();

    public GainBalances(SessionGains session) => this.session = session;

    public void Clear() => balances.Clear();

    public void Forget(int slot) => balances.Remove(slot);

    public void Observe(int slot, long owner, GainCurrency currency, long balance, bool record)
    {
        if (owner == 0 || balance < 0)
        {
            Forget(slot);
            return;
        }
        if (
            record
            && balances.TryGetValue(slot, out var previous)
            && previous.Owner == owner
            && previous.Currency == currency
        )
            session.RecordIncrease(currency, previous.Balance, balance);
        // Spending, paused play and manual grants also advance the baseline.
        balances[slot] = (owner, currency, balance);
    }
}
