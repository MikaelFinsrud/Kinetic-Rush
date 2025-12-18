public interface ICoinHitReceiver
{
    bool CanBeHit(CoinTargetDefinitionSO definition, in CoinHitContext ctx);
    void OnHit(CoinTargetDefinitionSO definition, in CoinHitContext ctx);
}
