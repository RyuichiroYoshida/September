using System;
using September.InGame.UI;
using UnityEngine;

namespace September.Common
{
	/// <summary>
	/// InGameDebugからゲーム開始時に適用する時間設定を保持します。
	/// </summary>
	[Serializable]
	public class InGameDebugTimeSettings
	{
		public int PreStartTime = 10;
		public float GameTime = 180f;

		/// <summary>
		/// 保持している時間設定をゲームのタイマーへ適用します。
		/// </summary>
		public void ApplyTo(GameTimerData timerData)
		{
			if (timerData == null)
				return;

			timerData.PreStartTime = Mathf.Max(0, PreStartTime);
			timerData.GameTime = Mathf.Max(0f, GameTime);
		}
	}

	/// <summary>
	/// InGameDebugの時間設定をゲーム開始時に一時適用します。
	/// </summary>
	public static class InGameDebugTimeInjector
	{
		private static InGameDebugTimeSettings _settings;
		private static GameTimerData _appliedTimerData;
		private static TimerDataValues _originalTimerData;

		/// <summary>
		/// 次回のゲーム開始時に適用する時間設定を登録します。
		/// </summary>
		public static void Set(InGameDebugTimeSettings settings)
		{
			_settings = settings;
		}

		/// <summary>
		/// 登録済みの時間設定をタイマーへ適用します。
		/// </summary>
		public static void Apply(GameTimerData timerData)
		{
			if (_settings == null || timerData == null)
				return;

			if (_appliedTimerData != timerData)
			{
				RestoreAppliedTimerData();
				_appliedTimerData = timerData;
				_originalTimerData = new TimerDataValues(timerData);
			}

			_settings.ApplyTo(timerData);
		}

		/// <summary>
		/// 登録した設定を解除し、変更したタイマーを元の値へ戻します。
		/// </summary>
		public static void Clear()
		{
			RestoreAppliedTimerData();
			_settings = null;
		}

		private static void RestoreAppliedTimerData()
		{
			if (_appliedTimerData != null)
				_originalTimerData.ApplyTo(_appliedTimerData);

			_appliedTimerData = null;
		}

		private readonly struct TimerDataValues
		{
			private readonly int _preStartTime;
			private readonly float _gameTime;

			public TimerDataValues(GameTimerData timerData)
			{
				_preStartTime = timerData.PreStartTime;
				_gameTime = timerData.GameTime;
			}

			public void ApplyTo(GameTimerData timerData)
			{
				timerData.PreStartTime = _preStartTime;
				timerData.GameTime = _gameTime;
			}
		}
	}
}
