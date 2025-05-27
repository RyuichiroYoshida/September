namespace September.Common
{
    public interface IPlayerRef
    {
        //Photonに依存しないPlayerRefのラッパー
        //使用例：IPlayerRef player = new PhotonPlayerRef(runner.LocalPlayer);
        T As<T>();
    }
}
