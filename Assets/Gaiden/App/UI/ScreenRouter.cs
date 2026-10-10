using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaiden.App
{
    /// <summary>A full-screen page. Build() is called each time it becomes visible, so it always reflects current state.</summary>
    public abstract class GameScreen
    {
        public VisualElement Root { get; private set; }
        public abstract string Id { get; }
        /// <summary>Music track for this screen (null = keep current).</summary>
        public virtual string Music => null;

        internal VisualElement Mount()
        {
            Root = new VisualElement();
            Root.AddToClassList("fill");
            Root.name = "screen-" + Id;
            Build(Root);
            return Root;
        }

        protected abstract void Build(VisualElement root);
        public virtual void OnShown() { }
        public virtual void OnHidden() { }
        public virtual void Tick(float dt) { }
        /// <summary>Return false to swallow the back action (e.g. cancel targeting first).</summary>
        public virtual bool OnBack() => true;

        /// <summary>Rebuilds in place (after a purchase etc.).</summary>
        public void Refresh()
        {
            if (Root == null) return;
            Root.Clear();
            Build(Root);
        }

        protected static GameApp App => GameApp.I;
    }

    /// <summary>Stack-based navigation with a fade. One screen is mounted at a time on the stage.</summary>
    public sealed class ScreenRouter
    {
        private readonly VisualElement host;
        private readonly VisualElement fade;
        private readonly Stack<GameScreen> stack = new Stack<GameScreen>();
        private bool busy;

        public GameScreen Current => stack.Count > 0 ? stack.Peek() : null;
        public IEnumerable<GameScreen> Stack => stack;
        public event Action<GameScreen> Changed;

        public ScreenRouter(VisualElement host, VisualElement fade)
        {
            this.host = host;
            this.fade = fade;
        }

        public void Push(GameScreen s) => Go(() => stack.Push(s));
        public void Replace(GameScreen s) => Go(() => { if (stack.Count > 0) stack.Pop().OnHidden(); stack.Push(s); });
        public void ResetTo(GameScreen s) => Go(() => { while (stack.Count > 0) stack.Pop().OnHidden(); stack.Push(s); });

        public void Pop()
        {
            if (stack.Count <= 1 || busy) return;
            if (!Current.OnBack()) return;
            Go(() => stack.Pop().OnHidden());
        }

        /// <summary>Back action from the hardware/escape key or a header back button.</summary>
        public void Back()
        {
            if (Current != null && !Current.OnBack()) return;
            if (stack.Count <= 1) return;
            Go(() => stack.Pop().OnHidden());
        }

        private void Go(Action mutate)
        {
            if (busy) { mutate(); MountTop(); return; }
            GameApp.I.StartCoroutine(Transition(mutate));
        }

        private IEnumerator Transition(Action mutate)
        {
            busy = true;
            bool hasScreen = host.childCount > 0;
            if (hasScreen)
            {
                fade.AddToClassList("fade--on");
                fade.pickingMode = PickingMode.Position;
                yield return new WaitForSecondsRealtime(0.2f);
            }
            mutate();
            MountTop();
            yield return null;
            fade.RemoveFromClassList("fade--on");
            fade.pickingMode = PickingMode.Ignore;
            busy = false;
        }

        private void MountTop()
        {
            host.Clear();
            var top = Current;
            if (top == null) return;
            host.Add(top.Mount());
            if (top.Music != null) GameApp.I.Audio.Music(top.Music);
            top.OnShown();
            Changed?.Invoke(top);
        }
    }
}
