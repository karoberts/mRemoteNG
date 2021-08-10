using System;
using System.Drawing;
using System.Windows.Forms;
using AxMSTSCLib;
using mRemoteNG.App;
using mRemoteNG.Messages;
using MSTSCLib;
using mRemoteNG.Resources.Language;

namespace mRemoteNG.Connection.Protocol.RDP
{
    /* RDP v8 requires Windows 7 with:
		* https://support.microsoft.com/en-us/kb/2592687 
		* OR
		* https://support.microsoft.com/en-us/kb/2923545
		* 
		* Windows 8+ support RDP v8 out of the box.
		*/
    public class RdpProtocol8 : RdpProtocol7
    {
        private MsRdpClient8NotSafeForScripting RdpClient8 => (MsRdpClient8NotSafeForScripting)((AxHost)Control).GetOcx();
        private Size _controlBeginningSize;

        protected override RdpVersion RdpProtocolVersion => RdpVersion.Rdc8;

        public override bool SmartSize
        {
            get => base.SmartSize;
            protected set
            {
                base.SmartSize = value;
                RdpClient8.AdvancedSettings2.SmartSizing = value;
                DoResizeControl();
                DoResizeClient();
            }
        }

        public override bool Fullscreen
        {
            get => base.Fullscreen;
            protected set
            {
                base.Fullscreen = value;
                DoResizeClient();
            }
        }

        public override bool Connect()
        {
            RdpClient8.AdvancedSettings2.SmartSizing = true;
            if (base.Connect())
            {
                DoResizeControl();
                return true;
            }
            return false;
        }

        public override void ResizeBegin(object sender, EventArgs e)
        {
            _controlBeginningSize = Control.Size;
        }

        public override void Resize(object sender, EventArgs e)
        {
            if (DoResizeControl() && _controlBeginningSize.IsEmpty)
            {
                DoResizeClient();
            }
            base.Resize(sender, e);
        }

        public override void ResizeEnd(object sender, EventArgs e)
        {
            DoResizeControl();
            if (!(Control.Size == _controlBeginningSize))
            {
                DoResizeClient();
            }
            _controlBeginningSize = Size.Empty;
        }

        protected override AxHost CreateActiveXRdpClientControl()
        {
            return new AxMsRdpClient8NotSafeForScripting();
        }

        private void DoResizeClient()
        {
            if (!loginComplete)
                return;

            if (!InterfaceControl.Info.AutomaticResize)
                return;

            /*
            if (!(InterfaceControl.Info.Resolution == RDPResolutions.FitToWindow ||
                  InterfaceControl.Info.Resolution == RDPResolutions.Fullscreen))
                return;
            */

            if (SmartSize)
                return;

            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Resizing RDP connection to host '{connectionInfo.Hostname}'");

            try
            {
                var size = Fullscreen
                    ? Screen.FromControl(Control).Bounds.Size
                    : Control.Size;
                UpdateSessionDisplaySettings((uint)size.Width, (uint)size.Height);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(
                    string.Format(Language.ChangeConnectionResolutionError,
                        connectionInfo.Hostname),
                    ex, MessageClass.WarningMsg, false);
            }
        }

        private bool DoResizeControl()
        {
            /*
            Control.Location = InterfaceControl.Location;
            // kmscode - this doesn't look right to me. But I'm not aware of any functionality issues with this currently...
            if (!(Control.Size == InterfaceControl.Size) && !(InterfaceControl.Size == Size.Empty))
            {
                Control.Size = InterfaceControl.Size;
                return true;
            }
            else
            {
                return false;
            }
            */
            return DoResize_KR();
        }
        
        protected virtual void UpdateSessionDisplaySettings(uint width, uint height)
        {
            RdpClient8.Reconnect(width, height);
        }        

        private bool DoResize_KR()
        {
            if (!SmartSize)
            {
                Control.Size = InterfaceControl.Size;
                Control.Location = InterfaceControl.Location;
                return true;
            }
            if (Control.FindForm().WindowState == FormWindowState.Minimized)
                return true;
            var resolution = GetResolutionRectangle(connectionInfo.Resolution);
            double ratioW = InterfaceControl.Parent.Size.Width / (double)resolution.Width;
            double ratioH = InterfaceControl.Parent.Size.Height / (double)resolution.Height;
            double scale = Math.Min(ratioW, ratioH);
            if (scale > 1.0d)
                return true;
            Control.Size = new Size((int)(resolution.Width * scale), (int)(resolution.Height * scale));
            var parentRect = Control.Parent.ClientRectangle;
            Control.Left = (parentRect.Width - Control.Width) / 2;
            Control.Top = (parentRect.Height - Control.Height) / 2;
            DoResizeClient();

            return true;
        }

		public static Rectangle GetResolutionRectangle(RDPResolutions resolution)
		{
			string[] resolutionParts = null;
			if (resolution != RDPResolutions.FitToWindow & resolution != RDPResolutions.Fullscreen & resolution != RDPResolutions.SmartSize)
			{
				resolutionParts = resolution.ToString().Replace("Res", "").Split('x');
			}
			if (resolutionParts == null || resolutionParts.Length != 2)
			{
				return new Rectangle(0, 0, 0, 0);
			}
			else
			{
                return new Rectangle(0, 0, Convert.ToInt32(resolutionParts[0]), Convert.ToInt32(resolutionParts[1]));
			}
		}
    }
}
