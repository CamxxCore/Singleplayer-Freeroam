using GTA;
using GTA.Native;

namespace SPFClient.UI
{
    public class UIBigMessage
    {
        Scaleform scaleform;
        private int messageWaitTime;
        private int messageWaitDuration;
        private int messageFadeDuration;
        private bool messageFading;
        private bool messageShowed;

        public UIBigMessage()
        {
            scaleform = new Scaleform(Function.Call<int>(Hash.REQUEST_SCALEFORM_MOVIE, "MP_BIG_MESSAGE_FREEMODE"));
        }

        public void Update()
        {
            if (messageShowed)
            {
                scaleform.Render2D();

                if (Game.GameTime >= messageWaitTime)
                {
                    scaleform.CallFunction("TRANSITION_OUT");
                    messageWaitTime = Game.GameTime + messageFadeDuration;
                    messageFading = true;
                    messageShowed = false;
                }
            }

            else if (messageFading)
            {
                scaleform.Render2D();

                if (Game.GameTime >= messageWaitTime)
                {
                    messageFading = false;
                }
            }
        }

        public void ShowMissionPassedMessage(string text, int duration, int fadeTime)
        {
            scaleform.CallFunction("SHOW_MISSION_PASSED_MESSAGE", text, "", 100, true, 0, true);
            messageWaitDuration = duration;
            messageFadeDuration = fadeTime;
            messageWaitTime = Game.GameTime + messageWaitDuration;
            messageShowed = true;
        }
    }
}
