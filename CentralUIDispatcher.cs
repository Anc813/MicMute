using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicMute
{
    static class CentralUIDispatcher
    {
        private delegate void ToggleMuteAction();
        private static ToggleMuteAction _toggleMute;
        private static MainForm _mainForm;

        static public void RegisterMainWindow(MainForm mainForm)
        {
            _mainForm = mainForm;
            _toggleMute =new ToggleMuteAction(ToggleMuteButton);
        }

        static private void ToggleMuteButton()
        {
            _mainForm.ToggleMicStatus();
        }

        static public void ProcessRequest(string text)
        {
            if (_mainForm != null)
                _mainForm.Invoke(_toggleMute);


        }
    }
}
