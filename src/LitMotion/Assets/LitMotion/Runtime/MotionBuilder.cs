using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LitMotion
{
    internal class MotionBuilderBuffer<TValue, TOptions>
        where TValue : unmanaged
        where TOptions : unmanaged, IMotionOptions
    {
        static MotionBuilderBuffer()
        {
            pool = new(4);
            for (int i = 0; i < 4; i++) pool.Push(new());
        }

        static readonly Stack<MotionBuilderBuffer<TValue, TOptions>> pool;

        public static MotionBuilderBuffer<TValue, TOptions> Rent()
        {
            if (!pool.TryPop(out var result)) result = new();
            return result;
        }

        public static void Return(MotionBuilderBuffer<TValue, TOptions> buffer)
        {
            buffer.Duration = default;
            buffer.Ease = default;
            buffer.IgnoreTimeScale = default;
            buffer.Delay = default;
            buffer.Loops = 1;
            buffer.LoopType = default;
            buffer.StartValue = default;
            buffer.EndValue = default;
            buffer.Options = default;
            buffer.Scheduler = default;
            buffer.OnComplete = default;
            buffer.IsPreserved = default;
            pool.Push(buffer);
        }

        public float Duration;
        public Ease Ease;
        public bool IgnoreTimeScale;
        public float Delay;
        public int Loops = 1;
        public LoopType LoopType;

        public TValue StartValue;
        public TValue EndValue;
        public TOptions Options;

        public IMotionScheduler Scheduler;

        public Action OnComplete;
        public bool IsPreserved;
    }

    /// <summary>
    /// Supports construction, scheduling, and binding of motion entities.
    /// </summary>
    /// <typeparam name="TValue">The type of value to animate</typeparam>
    /// <typeparam name="TOptions">The type of special parameters given to the motion data</typeparam>
    /// <typeparam name="TAdapter">The type of adapter that support value animation</typeparam>
    public struct MotionBuilder<TValue, TOptions, TAdapter> : IDisposable
        where TValue : unmanaged
        where TOptions : unmanaged, IMotionOptions
        where TAdapter : unmanaged, IMotionAdapter<TValue, TOptions>
    {
        internal MotionBuilder(MotionBuilderBuffer<TValue, TOptions> buffer)
        {
            this.buffer = buffer;
        }

        internal MotionBuilderBuffer<TValue, TOptions> buffer;

        /// <summary>
        /// Specify easing for motion.
        /// </summary>
        /// <param name="ease">The type of easing</param>
        /// <returns>This builder to allow chaining multiple method calls.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly MotionBuilder<TValue, TOptions, TAdapter> WithEase(Ease ease)
        {
            CheckBuffer();
            buffer.Ease = ease;
            return this;
        }

        /// <summary>
        /// Specify whether motion ignores time scale.
        /// </summary>
        /// <param name="ignoreTimeScale">If true, time scale will be ignored</param>
        /// <returns>This builder to allow chaining multiple method calls.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly MotionBuilder<TValue, TOptions, TAdapter> WithIgnoreTimeScale(bool ignoreTimeScale = true)
        {
            CheckBuffer();
            buffer.IgnoreTimeScale = ignoreTimeScale;
            return this;
        }

        /// <summary>
        /// Specify the delay time when the motion starts.
        /// </summary>
        /// <param name="delay">Delay time (seconds)</param>
        /// <returns>This builder to allow chaining multiple method calls.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly MotionBuilder<TValue, TOptions, TAdapter> WithDelay(float delay)
        {
            CheckBuffer();
            buffer.Delay = delay;
            return this;
        }

        /// <summary>
        /// Specify the number of times the motion is repeated. If specified as less than 0, the motion will continue to play until manually completed or canceled.
        /// </summary>
        /// <param name="loops">Number of loops</param>
        /// <param name="loopType">Behavior at the end of each loop</param>
        /// <returns>This builder to allow chaining multiple method calls.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly MotionBuilder<TValue, TOptions, TAdapter> WithLoops(int loops, LoopType loopType = LoopType.Restart)
        {
            CheckBuffer();
            buffer.Loops = loops;
            buffer.LoopType = loopType;
            return this;
        }

        /// <summary>
        /// Specify special parameters for each motion data.
        /// </summary>
        /// <param name="options">Option value to specify</param>
        /// <returns>This builder to allow chaining multiple method calls.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly MotionBuilder<TValue, TOptions, TAdapter> WithOptions(TOptions options)
        {
            CheckBuffer();
            buffer.Options = options;
            return this;
        }

        /// <summary>
        /// Specify the callback when playback ends.
        /// </summary>
        /// <param name="callback">Callback when playback ends</param>
        /// <returns>This builder to allow chaining multiple method calls.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly MotionBuilder<TValue, TOptions, TAdapter> WithOnComplete(Action callback)
        {
            CheckBuffer();
            buffer.OnComplete += callback;
            return this;
        }

        /// <summary>
        /// Specifies the scheduler that schedule the motion.
        /// </summary>
        /// <param name="scheduler">Scheduler</param>
        /// <returns>This builder to allow chaining multiple method calls.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly MotionBuilder<TValue, TOptions, TAdapter> WithScheduler(IMotionScheduler scheduler)
        {
            CheckBuffer();
            buffer.Scheduler = scheduler;
            return this;
        }

        /// <summary>
        /// Create motion and play it without binding it to a specific object.
        /// </summary>
        /// <returns>Handle of the created motion data.</returns>
        public MotionHandle RunWithoutBinding()
        {
            CheckBuffer();
            var callbacks = default(MotionCallbackData);
            callbacks.OnCompleteAction = buffer.OnComplete;
            var scheduler = buffer.Scheduler;
            var data = BuildMotionData();
            return Schedule(scheduler, data, callbacks);
        }

        /// <summary>
        /// Create motion and bind it to a specific object, property, etc.
        /// </summary>
        /// <param name="action">Action that handles binding</param>
        /// <returns>Handle of the created motion data.</returns>
        public MotionHandle Bind(Action<TValue> action)
        {
            CheckBuffer();
            var callbacks = MotionCallbackData.Create(action);
            callbacks.OnCompleteAction = buffer.OnComplete;
            var scheduler = buffer.Scheduler;
            var data = BuildMotionData();
            return Schedule(scheduler, data, callbacks);
        }

        /// <summary>
        /// Create motion and bind it to a specific object. Unlike the regular Bind method, it avoids allocation by closure by passing an object.
        /// </summary>
        /// <typeparam name="TState">Type of state</typeparam>
        /// <param name="state">Motion state</param>
        /// <param name="action">Action that handles binding</param>
        /// <returns>Handle of the created motion data.</returns>
        public MotionHandle BindWithState<TState>(TState state, Action<TValue, TState> action) where TState : class
        {
            CheckBuffer();
            var callbacks = MotionCallbackData.Create(state, action);
            callbacks.OnCompleteAction = buffer.OnComplete;
            var scheduler = buffer.Scheduler;
            var data = BuildMotionData();
            return Schedule(scheduler, data, callbacks);
        }

        /// <summary>
        /// Preserves the internal buffer and prevents the builder from being automatically destroyed after creating the motion data.
        /// Calling this allows you to create the motion multiple times, but you must call the Dispose method to destroy the builder after use.
        /// </summary>
        /// <returns>This builder to allow chaining multiple method calls.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly MotionBuilder<TValue, TOptions, TAdapter> Preserve()
        {
            CheckBuffer();
            buffer.IsPreserved = true;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly MotionHandle Schedule(IMotionScheduler scheduler, in MotionData<TValue, TOptions> data, in MotionCallbackData callbackData)
        {
            if (scheduler == null)
            {
                return MotionDispatcher.Schedule<TValue, TOptions, TAdapter>(data, callbackData, UpdateMode.Update);
            }
            else
            {
                return scheduler.Schedule<TValue, TOptions, TAdapter>(data, callbackData);
            }
        }

        /// <summary>
        /// Dispose this builder. You need to call this manually after calling Preserve or if you have never created a motion data.
        /// </summary>
        public void Dispose()
        {
            if (buffer == null) return;
            MotionBuilderBuffer<TValue, TOptions>.Return(buffer);
            buffer = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal MotionData<TValue, TOptions> BuildMotionData()
        {
            var data = new MotionData<TValue, TOptions>()
            {
                StartValue = buffer.StartValue,
                EndValue = buffer.EndValue,
                Options = buffer.Options,
                Duration = buffer.Duration,
                Ease = buffer.Ease,
                IgnoreTimeScale = buffer.IgnoreTimeScale,
                Delay = buffer.Delay,
                Loops = buffer.Loops,
                LoopType = buffer.LoopType,
                Status = MotionStatus.Scheduled,
            };
            if (!buffer.IsPreserved) Dispose();
            return data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void CheckBuffer()
        {
            if (buffer == null) throw new InvalidOperationException("MotionBuilder is either not initialized or has already run a Build (or Bind). If you want to build or bind multiple times, call Preseve() for MotionBuilder.");
        }
    }
}