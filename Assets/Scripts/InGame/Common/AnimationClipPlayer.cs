using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace InGame.Common
{
    public class AnimationClipPlayer : MonoBehaviour
    {
        [SerializeField] protected Animator _animator;
    
        private PlayableGraph _graph;
        private AnimationLayerMixerPlayable _layerMixer;
        private const int PlayClipLayerIndex = 1;

        void Start()
        {
            // PlayableGraph構築
            _graph = PlayableGraph.Create("CharacterGraph");
            //_graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            // AnimatorControllerPlayableを作成（レイヤー0）
            var controllerPlayable = AnimatorControllerPlayable.Create(_graph, _animator.runtimeAnimatorController);

            // AnimationLayerMixerPlayable（2レイヤー：0 = AnimController, 1 = PlayClip）
            _layerMixer = AnimationLayerMixerPlayable.Create(_graph, 2);
            _layerMixer.ConnectInput(0, controllerPlayable, 0);

            // 出力設定
            var output = AnimationPlayableOutput.Create(_graph, "Output", _animator);
            output.SetSourcePlayable(_layerMixer);

            // スキル用レイヤーを初期化（無効化）
            _layerMixer.SetInputWeight(0, 1f);
            _layerMixer.SetInputWeight(PlayClipLayerIndex, 0f);
            //_layerMixer.SetLayerAdditive(PlayClipLayerIndex, false);

            _graph.Play();
        }

        public void PlayClip(AnimationClip clip, bool applyRootMotion = false)
        {
            //Debug.Log($"Play clip : {clip.name}");
            // スキル用 AnimationClipPlayable を作成
            var skillPlayable = AnimationClipPlayable.Create(_graph, clip);
            skillPlayable.SetApplyFootIK(true);
            //skillPlayable.SetTime(0);
            //skillPlayable.SetDuration(clip.length);
            //skillPlayable.SetSpeed(1);

            // ミキサーのスキル用レイヤーに接続（既存がある場合は置き換え）
            _layerMixer.DisconnectInput(PlayClipLayerIndex);
            _layerMixer.ConnectInput(PlayClipLayerIndex, skillPlayable, 0);
            _layerMixer.SetInputWeight(PlayClipLayerIndex, 1f);

            // 終了後に戻す
            StartCoroutine(DisableSkillLayerAfter(clip.length));
        }

        // public UniTask PlayClipAsync(AnimationClip clip, bool applyRootMotion = false)
        // {
        //     var tcs = new TaskCompletionSource<object>();
        //     
        //     return ;
        // }

        private System.Collections.IEnumerator DisableSkillLayerAfter(float time)
        {
            yield return new WaitForSeconds(time);
            _layerMixer.SetInputWeight(PlayClipLayerIndex, 0f);
            _layerMixer.DisconnectInput(PlayClipLayerIndex);
        }

        void OnDestroy()
        {
            _graph.Destroy();
        }
    }
}
