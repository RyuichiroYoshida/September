#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace September.EditorExtension
{
    /// <summary>
    /// Humanoidの足軌跡をサンプリングし、接地中の足が滑らない1倍速時の移動速度を推定する。
    /// </summary>
    public static class LocoAnimSpeedEstimator
    {
        private const int MinSampleRate = 30;
        private const int MaxSampleRate = 240;
        private const float MinimumEstimatedSpeed = 0.05f;

        /// <summary>
        /// 指定したAvatarでクリップを再生し、足の接地区間から基準速度を推定する。
        /// </summary>
        public static LocoAnimSpeedEstimate Estimate(Animator sourceAnimator, AnimationClip clip, int sampleRate)
        {
            if (sourceAnimator == null)
                return LocoAnimSpeedEstimate.Failure("Animatorが設定されていません。");
            if (clip == null)
                return LocoAnimSpeedEstimate.Failure("AnimationClipが設定されていません。");
            if (!sourceAnimator.isHuman || sourceAnimator.avatar == null || !sourceAnimator.avatar.isValid)
                return LocoAnimSpeedEstimate.Failure("有効なHumanoid Avatarが必要です。");
            if (clip.length <= Mathf.Epsilon)
                return LocoAnimSpeedEstimate.Failure("長さが0秒のクリップは解析できません。");

            int clampedSampleRate = Mathf.Clamp(sampleRate, MinSampleRate, MaxSampleRate);
            if (!TrySampleFootPositions(
                    sourceAnimator,
                    clip,
                    clampedSampleRate,
                    out var leftPositions,
                    out var rightPositions,
                    out float sampleDeltaTime,
                    out string error))
            {
                return LocoAnimSpeedEstimate.Failure(error);
            }

            float humanScale = Mathf.Max(0.1f, sourceAnimator.humanScale);
            var leftEstimate = EstimateFootSpeed(leftPositions, sampleDeltaTime, humanScale);
            var rightEstimate = EstimateFootSpeed(rightPositions, sampleDeltaTime, humanScale);

            if (!leftEstimate.IsValid && !rightEstimate.IsValid)
            {
                return LocoAnimSpeedEstimate.Failure(
                    "接地中に後方へ動く足を検出できませんでした。直進する歩行・走行クリップか確認してください。");
            }

            return CombineEstimates(leftEstimate, rightEstimate, leftPositions.Count);
        }

        private static bool TrySampleFootPositions(
            Animator sourceAnimator,
            AnimationClip clip,
            int sampleRate,
            out List<Vector3> leftPositions,
            out List<Vector3> rightPositions,
            out float sampleDeltaTime,
            out string error)
        {
            leftPositions = new List<Vector3>();
            rightPositions = new List<Vector3>();
            sampleDeltaTime = 0f;
            error = string.Empty;

            GameObject sampleObject = null;
            var graph = default(PlayableGraph);

            try
            {
                sampleObject = new GameObject(sourceAnimator.gameObject.name);
                sampleObject.hideFlags = HideFlags.HideAndDontSave;
                sampleObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                sampleObject.transform.localScale = sourceAnimator.transform.lossyScale;

                // GameplayコンポーネントのAwake/OnEnableを実行しないよう、Transform階層だけを再構築する。
                CloneTransformChildren(sourceAnimator.transform, sampleObject.transform);

                var sampleAnimator = sampleObject.AddComponent<Animator>();
                sampleAnimator.avatar = sourceAnimator.avatar;
                sampleAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                sampleAnimator.applyRootMotion = false;
                sampleAnimator.fireEvents = false;
                sampleAnimator.runtimeAnimatorController = null;

                var leftFoot = sampleAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var rightFoot = sampleAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (leftFoot == null || rightFoot == null)
                {
                    error = "Humanoid Avatarから左右の足ボーンを取得できませんでした。";
                    return false;
                }

                graph = PlayableGraph.Create("LocoAnimSpeedEstimator");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

                var output = AnimationPlayableOutput.Create(graph, "AnimationOutput", sampleAnimator);
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                playable.SetSpeed(0d);
                output.SetSourcePlayable(playable);
                graph.Play();

                int sampleCount = Mathf.Max(3, Mathf.CeilToInt(clip.length * sampleRate));
                sampleDeltaTime = clip.length / sampleCount;
                leftPositions.Capacity = sampleCount;
                rightPositions.Capacity = sampleCount;

                for (int i = 0; i < sampleCount; i++)
                {
                    double sampleTime = i * sampleDeltaTime;
                    playable.SetTime(sampleTime);
                    graph.Evaluate(0f);

                    leftPositions.Add(ToAnimatorSpaceInMeters(sampleAnimator.transform, leftFoot.position));
                    rightPositions.Add(ToAnimatorSpaceInMeters(sampleAnimator.transform, rightFoot.position));
                }

                return true;
            }
            catch (Exception exception)
            {
                error = $"アニメーションのサンプリングに失敗しました: {exception.Message}";
                return false;
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                if (sampleObject != null) UnityEngine.Object.DestroyImmediate(sampleObject);
            }
        }

        private static void CloneTransformChildren(Transform sourceParent, Transform destinationParent)
        {
            for (int i = 0; i < sourceParent.childCount; i++)
            {
                Transform sourceChild = sourceParent.GetChild(i);
                var destinationObject = new GameObject(sourceChild.name)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                Transform destinationChild = destinationObject.transform;
                destinationChild.SetParent(destinationParent, false);
                destinationChild.localPosition = sourceChild.localPosition;
                destinationChild.localRotation = sourceChild.localRotation;
                destinationChild.localScale = sourceChild.localScale;

                CloneTransformChildren(sourceChild, destinationChild);
            }
        }

        private static FootSpeedEstimate EstimateFootSpeed(
            IReadOnlyList<Vector3> positions,
            float sampleDeltaTime,
            float humanScale)
        {
            if (positions.Count < 3 || sampleDeltaTime <= 0f) return FootSpeedEstimate.Invalid;

            float minHeight = float.PositiveInfinity;
            float maxHeight = float.NegativeInfinity;
            for (int i = 0; i < positions.Count; i++)
            {
                minHeight = Mathf.Min(minHeight, positions[i].y);
                maxHeight = Mathf.Max(maxHeight, positions[i].y);
            }

            float heightRange = Mathf.Max(0f, maxHeight - minHeight);
            float contactHeight = Mathf.Max(0.025f * humanScale, heightRange * 0.22f);
            float verticalSpeedLimit = Mathf.Max(0.2f, 0.65f * humanScale);
            float backwardSpeedMinimum = Mathf.Max(0.03f, 0.05f * humanScale);
            var samples = new List<WeightedSpeed>();

            for (int i = 1; i < positions.Count - 1; i++)
            {
                Vector3 velocity = (positions[i + 1] - positions[i - 1]) / (2f * sampleDeltaTime);
                float heightAboveLowestPoint = positions[i].y - minHeight;
                float backwardSpeed = -velocity.z;

                if (heightAboveLowestPoint > contactHeight) continue;
                if (Mathf.Abs(velocity.y) > verticalSpeedLimit) continue;
                if (backwardSpeed < backwardSpeedMinimum) continue;

                float heightWeight = 1f - Mathf.Clamp01(heightAboveLowestPoint / contactHeight);
                float verticalWeight = 1f - Mathf.Clamp01(Mathf.Abs(velocity.y) / verticalSpeedLimit);
                float weight = Mathf.Max(0.05f, heightWeight * verticalWeight);
                samples.Add(new WeightedSpeed(backwardSpeed, weight));
            }

            if (samples.Count < 3) return FootSpeedEstimate.Invalid;

            float speed = WeightedMedian(samples);
            var deviations = new List<WeightedSpeed>(samples.Count);
            for (int i = 0; i < samples.Count; i++)
            {
                deviations.Add(new WeightedSpeed(Mathf.Abs(samples[i].Speed - speed), samples[i].Weight));
            }

            float medianAbsoluteDeviation = WeightedMedian(deviations);
            float expectedContactSamples = Mathf.Max(3f, positions.Count * 0.15f);
            float coverage = Mathf.Clamp01(samples.Count / expectedContactSamples);
            float allowedDeviation = Mathf.Max(0.05f, speed * 0.25f);
            float consistency = 1f - Mathf.Clamp01(medianAbsoluteDeviation / allowedDeviation);
            float confidence = consistency * 0.6f + coverage * 0.4f;

            return new FootSpeedEstimate(speed, confidence, samples.Count);
        }

        private static LocoAnimSpeedEstimate CombineEstimates(
            FootSpeedEstimate left,
            FootSpeedEstimate right,
            int totalSamplesPerFoot)
        {
            if (!left.IsValid)
            {
                return CreateSingleFootEstimate(right, false, totalSamplesPerFoot);
            }

            if (!right.IsValid)
            {
                return CreateSingleFootEstimate(left, true, totalSamplesPerFoot);
            }

            float confidenceSum = left.Confidence + right.Confidence;
            float speed = confidenceSum > 0f
                ? (left.Speed * left.Confidence + right.Speed * right.Confidence) / confidenceSum
                : (left.Speed + right.Speed) * 0.5f;
            float agreement = 1f - Mathf.Clamp01(Mathf.Abs(left.Speed - right.Speed) / Mathf.Max(speed, MinimumEstimatedSpeed));
            float confidence = Mathf.Clamp01((left.Confidence + right.Confidence) * 0.35f + agreement * 0.3f);

            if (speed < MinimumEstimatedSpeed)
            {
                return LocoAnimSpeedEstimate.Failure("推定速度が小さすぎます。in-placeクリップの足軌跡を確認してください。");
            }

            return LocoAnimSpeedEstimate.Success(
                speed,
                confidence,
                left.Speed,
                right.Speed,
                left.ContactSampleCount + right.ContactSampleCount,
                totalSamplesPerFoot * 2);
        }

        private static LocoAnimSpeedEstimate CreateSingleFootEstimate(
            FootSpeedEstimate estimate,
            bool isLeft,
            int totalSamplesPerFoot)
        {
            if (estimate.Speed < MinimumEstimatedSpeed)
            {
                return LocoAnimSpeedEstimate.Failure("推定速度が小さすぎます。in-placeクリップの足軌跡を確認してください。");
            }

            float leftSpeed = isLeft ? estimate.Speed : 0f;
            float rightSpeed = isLeft ? 0f : estimate.Speed;
            return LocoAnimSpeedEstimate.Success(
                estimate.Speed,
                estimate.Confidence * 0.65f,
                leftSpeed,
                rightSpeed,
                estimate.ContactSampleCount,
                totalSamplesPerFoot * 2);
        }

        private static float WeightedMedian(List<WeightedSpeed> samples)
        {
            samples.Sort((a, b) => a.Speed.CompareTo(b.Speed));

            float totalWeight = 0f;
            for (int i = 0; i < samples.Count; i++) totalWeight += samples[i].Weight;

            float halfWeight = totalWeight * 0.5f;
            float accumulatedWeight = 0f;
            for (int i = 0; i < samples.Count; i++)
            {
                accumulatedWeight += samples[i].Weight;
                if (accumulatedWeight >= halfWeight) return samples[i].Speed;
            }

            return samples[^1].Speed;
        }

        private static Vector3 ToAnimatorSpaceInMeters(Transform animatorTransform, Vector3 worldPosition)
        {
            Vector3 worldOffset = worldPosition - animatorTransform.position;
            return Quaternion.Inverse(animatorTransform.rotation) * worldOffset;
        }

        private readonly struct WeightedSpeed
        {
            public readonly float Speed;
            public readonly float Weight;

            public WeightedSpeed(float speed, float weight)
            {
                Speed = speed;
                Weight = weight;
            }
        }

        private readonly struct FootSpeedEstimate
        {
            public static FootSpeedEstimate Invalid => default;

            public readonly bool IsValid;
            public readonly float Speed;
            public readonly float Confidence;
            public readonly int ContactSampleCount;

            public FootSpeedEstimate(float speed, float confidence, int contactSampleCount)
            {
                IsValid = true;
                Speed = speed;
                Confidence = confidence;
                ContactSampleCount = contactSampleCount;
            }
        }
    }

    /// <summary>足軌跡から算出した1倍速時の移動速度と解析品質。</summary>
    public readonly struct LocoAnimSpeedEstimate
    {
        public bool IsValid { get; }
        public string Error { get; }
        public float Speed { get; }
        public float Confidence { get; }
        public float LeftFootSpeed { get; }
        public float RightFootSpeed { get; }
        public int ContactSampleCount { get; }
        public int TotalSampleCount { get; }

        private LocoAnimSpeedEstimate(
            bool isValid,
            string error,
            float speed,
            float confidence,
            float leftFootSpeed,
            float rightFootSpeed,
            int contactSampleCount,
            int totalSampleCount)
        {
            IsValid = isValid;
            Error = error;
            Speed = speed;
            Confidence = confidence;
            LeftFootSpeed = leftFootSpeed;
            RightFootSpeed = rightFootSpeed;
            ContactSampleCount = contactSampleCount;
            TotalSampleCount = totalSampleCount;
        }

        /// <summary>解析成功結果を生成する。</summary>
        public static LocoAnimSpeedEstimate Success(
            float speed,
            float confidence,
            float leftFootSpeed,
            float rightFootSpeed,
            int contactSampleCount,
            int totalSampleCount)
        {
            return new LocoAnimSpeedEstimate(
                true,
                string.Empty,
                speed,
                confidence,
                leftFootSpeed,
                rightFootSpeed,
                contactSampleCount,
                totalSampleCount);
        }

        /// <summary>解析失敗結果を生成する。</summary>
        public static LocoAnimSpeedEstimate Failure(string error)
        {
            return new LocoAnimSpeedEstimate(false, error, 0f, 0f, 0f, 0f, 0, 0);
        }
    }
}
#endif
