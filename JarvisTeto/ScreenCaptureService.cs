using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace JarvisTeto.Services
{
    public static class ScreenCaptureService
    {
        /// <summary>
        /// Devuelve los límites (en píxeles físicos) del monitor donde vive actualmente la ventana
        /// dada, aunque esté oculta. Se usa para que Jarvis solo "vea" el monitor donde él está,
        /// no otros monitores conectados.
        /// </summary>
        public static System.Drawing.Rectangle GetOwnerScreenBounds(Window window)
        {
            try
            {
                var handle = new WindowInteropHelper(window).Handle;
                var screen = Screen.FromHandle(handle);
                return screen.Bounds;
            }
            catch
            {
                return Screen.PrimaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
            }
        }

        /// <summary>
        /// Captura únicamente la región de pantalla indicada (un monitor, no todos) y devuelve el PNG en base64.
        /// </summary>
        public static string CaptureRegionAsBase64Png(System.Drawing.Rectangle bounds)
        {
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }

            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }
    }
}
