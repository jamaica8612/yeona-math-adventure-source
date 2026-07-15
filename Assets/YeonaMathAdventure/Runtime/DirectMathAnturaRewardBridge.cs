namespace YeonaMathAdventure
{
    using YeonaMathAdventure.AnturaBridge;

    /// <summary>
    /// Drop-in replacement for ReflectionAnturaRewardBridge after both staging folders are
    /// integrated into Assembly-CSharp.
    /// </summary>
    public sealed class DirectMathAnturaRewardBridge : IMathRewardBridge
    {
        public bool TryAward(int amount)
        {
            MathAnturaBridge bridge = MathAnturaBridge.Instance;
            return bridge != null && bridge.TryGrantMathSuccessBones(amount);
        }
    }
}
