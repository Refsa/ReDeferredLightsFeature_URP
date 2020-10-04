// (c) 2006-2011 John P. Costella.
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
namespace Costella
{
    public static class Magic<T>
    {
        public interface IAccessible { T this[int n] { get; set; } }
        public interface IUpDown : Magic.IUpDown, IAccessible { }
        public interface IFull : Magic.IFull, IAccessible { }
        public static void Upsample(IUpDown o)
        {
            for (var i = 0; i < o.DestLength; i++)
            {
                var j = (i >> 1);
                var k = j - 1 + ((i & 1) << 1);
                o[i] = C13(o[k], o[j]);
            }
        }
        public static void Downsample(IUpDown o)
        {
            for (var i = 0; i < o.DestLength; i++)
            {
                var j = (i << 1);
                o[i] = C1331(o[j - 1], o[j], o[j + 1], o[j + 2]);
            }
        }
        public static void Resample(IFull o)
        {
            var ks = Magic.Kernels(o);
            for (var i = 0; i < o.DestLength; i++)
            {
                T sum = Zero;
                var k = ks[i];
                var m = 0;
                for (var j = k.Min; j <= k.Max; j++, m++)
                    sum = MultAdd(k[m], o[j], sum);
                o[i] = sum;
            }
        }
        public static Func<T, T, T> C13; public static Func<T, T, T, T, T> C1331; public static T Zero; public static Func<double, T, T, T> MultAdd;
    }
    public static class Magic
    {
        public static void Upsample<T>(this Magic<T>.IUpDown o) { Magic<T>.Upsample(o); }
        public static void Downsample<T>(this Magic<T>.IUpDown o) { Magic<T>.Downsample(o); }
        public static void Resample<T>(this Magic<T>.IFull o) { Magic<T>.Resample(o); }
        public interface IUpDown { int DestLength { get; } }
        public interface IFull : IUpDown
        {
            double SrcOrigin { get; }
            double SrcSpacing { get; }
            double DestOrigin { get; }
            double DestSpacing { get; }
        }
        public static Ks Kernels(IFull o)
        {
            var key = Key(o);
            return kernels.GetOrAdd(key, k => new Lazy<Ks>(() => new Ks(k))).Value;
        }
        public class K
        {
            public int Min;
            public int Max;
            public double this[int i] => Weights[i];
            public double[] Weights;
        }
        public class Ks
        {
            public Ks(Tuple<double, double, int> key)
            {
                var step = key.Item1; var start = key.Item2; var len = key.Item3; ks = new K[len];
                for (var j = 0; j < len; j++)
                {
                    var x = start + j * step; var k = ks[j] = new K();
                    if (step < 1)
                    {
                        var i = (int)Math.Round(x); k.Min = i - 1; k.Max = i + 1;
                        var ws = k.Weights = new double[3];
                        var f = x - i; var fm = f - 0.5; var fp = f + 0.5; ws[0] = 0.5 * fm * fm; ws[1] = 0.75 - f * f; ws[2] = 0.5 * fp * fp;
                    }
                    else
                    {
                        var hw = 1.5 * step; k.Min = (int)Math.Ceiling(x - hw); k.Max = (int)Math.Floor(x + hw);
                        var n = k.Max - k.Min + 1; var ws = k.Weights = new double[n];
                        var m = 0; double sum = 0; for (var i = k.Min; i <= k.Max; i++, m++) { var w = ws[m] = Weight((i - x) / step); sum += w; }
                        var scale = 1 / sum; for (m = 0; m < n; m++) ws[m] *= scale;
                    }
                }
            }
            public K this[int i] => ks[i];
            K[] ks;
        }
        public static double Weight(double x)
        {
            if (x < -1.5 || x > 1.5) return 0;
            if (x < -0.5)
            {
                var z = x + 1.5; return 0.5 * z * z;
            }
            if (x > 0.5)
            {
                var z = x - 1.5; return 0.5 * z * z;
            }
            return 0.75 - x * x;
        }
        public static double Step(double x)
        {
            if (x < -1) return 1;
            if (x < 0)
            {
                var z = x + 1;
                return 1 - 0.5 * z * z;
            }
            if (x < 1)
            {
                var z = x - 1;
                return 0.5 * z * z;
            }
            return 0;
        }
        static ConcurrentDictionary<Tuple<double, double, int>, Lazy<Ks>> kernels = new ConcurrentDictionary<Tuple<double, double, int>, Lazy<Ks>>();
        static Tuple<double, double, int> Key(IFull o) { return new Tuple<double, double, int>(o.DestSpacing / o.SrcSpacing, ((o.DestOrigin + o.DestSpacing / 2) - (o.SrcOrigin + o.SrcSpacing / 2)) / o.SrcSpacing, o.DestLength); }
        static Magic()
        {
            Magic<byte>.C13 = (a, b) => (byte)((a + 3 * b + 2) >> 2);
            Magic<byte>.C1331 = (a, b, c, d) => (byte)((a + 3 * (b + c) + d + 4) >> 3);

            Magic<int>.C13 = (a, b) => ((a + 3 * b + 2) >> 2);
            Magic<int>.C1331 = (a, b, c, d) => ((a + 3 * (b + c) + d + 4) >> 3);

            Magic<double>.C13 = (a, b) => (a + 3 * b) / 4;
            Magic<double>.C1331 = (a, b, c, d) => (a + 3 * (b + c) + d) / 8;
            Magic<double>.Zero = 0;
            Magic<double>.MultAdd = (a, b, c) => a * b + c;
        }
    }
}
