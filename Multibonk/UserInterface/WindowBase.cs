using UnityEngine;

namespace Multibonk.UserInterface
{
    public abstract class WindowBase
    {
        protected Rect windowRect;
        private bool dragging = false;
        private Vector2 dragOffset;

        protected WindowBase(Rect initialRect)
        {
            windowRect = initialRect;
        }

        public void Handle()
        {
            Utils.HandleWindowDrag(ref windowRect, ref dragging, ref dragOffset);

            // Keep the window on screen
            windowRect.x = Mathf.Clamp(windowRect.x, 0, Mathf.Max(0, Screen.width - windowRect.width));
            windowRect.y = Mathf.Clamp(windowRect.y, 0, Mathf.Max(0, Screen.height - windowRect.height));

            RenderWindow(windowRect);

            GUI.color = Color.white;
        }

        protected abstract void RenderWindow(Rect rect);
    }
}
