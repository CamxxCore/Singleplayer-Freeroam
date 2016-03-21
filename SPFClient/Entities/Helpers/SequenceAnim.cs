using System.Linq;
using GTA;
using GTA.Native;
using SPFClient.Types;

namespace SPFClient.Entities
{
    public sealed class SequenceAnim
    {
        private Animation[] animArgs;

        public Animation[] Animations { get { return animArgs; } }

        public SequenceAnim(params Animation[] args)
        {
            animArgs = args;
        }

        public void StartAnimation(Ped ped)
        {
            var taskseq = new TaskSequence();

            foreach (var anim in animArgs)
            {
                if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, anim.Dictionary))
                    Function.Call(Hash.REQUEST_ANIM_DICT, anim.Dictionary);
                taskseq.AddTask.PlayAnimation(anim.Dictionary, anim.Name, -8.0f, -1, false, 1f);
            }

            taskseq.Close();

            ped.Task.PerformSequence(taskseq);

            taskseq.Dispose();
        }

        public bool IsPlayingOn(Ped ped)
        {
            return animArgs.Any(x => Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, ped.Handle, x.Dictionary, x.Name, 3));
        }
    }
}
