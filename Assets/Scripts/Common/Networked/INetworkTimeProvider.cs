
namespace September.Common
{
    public interface INetworkTimeProvider
    {
        /// <summary>
        /// 現在の論理時刻（秒）を返す
        /// </summary>
        float GetTime();
    }
}

