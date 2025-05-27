using Fusion;

namespace September.Common
{
    public class PhotonTimeProvider : INetworkTimeProvider
    {
        private readonly NetworkRunner _runner;

        public PhotonTimeProvider(NetworkRunner runner)
        {
            _runner = runner;
        }

        public float GetTime()
        {
            return _runner.Tick * _runner.DeltaTime;
        }
    }
}
