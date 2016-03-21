using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SPFClient.Network
{
    public static class Reliability
    {
        public static uint BitIndexForSequence(uint sequence, uint ack, uint max_sequence)
        {
            Debug.Assert(sequence != ack);
            Debug.Assert(!ValidateSequence(sequence, ack, max_sequence));
            if (sequence > ack)
            {
                Debug.Assert(ack < 33);
                Debug.Assert(max_sequence >= sequence);
                return ack + (max_sequence - sequence);
            }
            else
            {
                Debug.Assert(ack >= 1);
                Debug.Assert(sequence <= ack - 1);
                return ack - 1 - sequence;
            }
        }

      //  private static uint GenerateAckBits(uint ack, SPFLib.Types.ClientState[] received_queue, uint max_sequence)
      /*  {
            uint ack_bits = 0;
            for (int i = 0; i < received_queue.Length; i++)
            {
                if (received_queue[i].Sequence == ack || ValidateSequence(received_queue[i].Sequence, ack, max_sequence))
                    break;
                int bit_index = (int)BitIndexForSequence(received_queue[i].Sequence, ack, max_sequence);
                if (bit_index <= 31)
                {
                    ack_bits |= 1 << bit_index;
                }
            }
            return ack_bits;
        }*/

        public static bool ValidateSequence(uint s1, uint s2, uint max)
        {
            return (s1 > s2) && (s1 - s2 <= max / 2) || (s2 > s1) && (s2 - s1 > max / 2);
        }
    }
}
