# Bake the same cell-local alpha feather formerly calculated on first orb selection.
param([string]$AssetDirectory = "$PSScriptRoot/../LastEpoch_Hud/Assets/Generated")
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Private.Windows.GdiPlus,System.Private.Windows.Core,System.Runtime.InteropServices -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class AttunementAtlasBake {
    public static void Bake(string source, string destination) {
        using (var original = new Bitmap(source))
        using (var bitmap = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb)) {
            using (var graphics = Graphics.FromImage(bitmap)) graphics.DrawImageUnscaled(original, 0, 0);
            var data = bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try {
                var pixels = new byte[data.Stride * bitmap.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                for (int y=0; y<bitmap.Height; y++) {
                    // Unity's source pixels run bottom to top.
                    double v = (((bitmap.Height - 1 - y) + 0.5) * 4.0 / bitmap.Height) % 1.0;
                    for (int x=0; x<bitmap.Width; x++) {
                        double u = ((x+0.5)*4.0/bitmap.Width)%1.0;
                        double dx=(u-0.5)*2, dy=(v-0.5)*2;
                        double edge=Math.Max(0,Math.Min(1,(Math.Sqrt(dx*dx+dy*dy)-0.72)/0.27));
                        double feather=1-edge*edge*(3-2*edge);
                        int index=y*data.Stride+x*4+3;
                        pixels[index]=(byte)(pixels[index]*feather);
                    }
                }
                Marshal.Copy(pixels,0,data.Scan0,pixels.Length);
            } finally { bitmap.UnlockBits(data); }
            bitmap.Save(destination,ImageFormat.Png);
        }
    }
}
"@
foreach($name in @('attunement_burst_sheet','attunement_negative_sheet','attunement_positive_sheet')) {
    [AttunementAtlasBake]::Bake((Join-Path $AssetDirectory ($name+'.png')), (Join-Path $AssetDirectory ($name+'_prepared.png')))
}