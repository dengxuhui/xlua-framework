using System;
using System.Collections.Generic;

namespace AssetBundles
{
    /// <summary>
    /// 弱引用工具
    /// </summary>
    public static class WeakReferenceUtility
    {
        private static readonly Queue<WeakReference> Pool = new Queue<WeakReference>();

        public static WeakReference Get(object target)
        {
            if (Pool.Count > 0)
            {
                var i = Pool.Dequeue();
                i.Target = target;
                return i;
            }
            else
            {
                return new WeakReference(target);
            }
        }

        public static void Recover(WeakReference w)
        {
            if (w == null)
            {
                return;
            }

            w.Target = null;
            Pool.Enqueue(w);
        }
    }
}