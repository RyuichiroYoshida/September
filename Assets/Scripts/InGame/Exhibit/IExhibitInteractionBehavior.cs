namespace InGame.Exhibit
{
    public interface IExhibitInteractionBehavior
    {
        // 爆弾設置
        void OnSetBomb();       
        // 破壊スキル
        void OnDestroyAbility();
        // 友好アクション
        void OnFriend();        
    }
}