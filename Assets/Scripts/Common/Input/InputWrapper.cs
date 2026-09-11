namespace September.Common.Input
{
    public struct InputWrapper
    {
        private bool _isActive;
        private bool _prevActive;
        
        /// <summary>
        /// 入力が開始された瞬間か
        /// </summary>
        public bool IsJustPressed => _isActive && !_prevActive;
        
        /// <summary>
        /// 入力中か
        /// </summary>
        public bool IsPressed => _isActive;

        /// <summary>
        /// 入力状態を更新
        /// </summary>
        public void SetInput(bool active)
        {
            _prevActive = _isActive;
            _isActive = active;
        }
    }
}