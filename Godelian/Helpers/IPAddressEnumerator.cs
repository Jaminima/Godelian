using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Helpers
{
    internal static class IPAddressEnumerator
    {
        public const uint FirstIPIndex = 16843009; //1.1.1.1
        public const uint LastIPIndex = 4294967294; //255.255.255.255

        public static IEnumerable<KeyValuePair<uint, string>> EnumerateIPRange(uint startIdx, int count)
        {
            for (uint i = startIdx; i < startIdx + count; i++)
            {
                yield return new KeyValuePair<uint, string>(i, GetIndexAsIP(i));
            }
        }

        public static string GetIndexAsIP(uint index)
        {
            int[] ipParts = new int[4];
            ipParts[0] = (int)((index >> 24) & 0xFF);
            ipParts[1] = (int)((index >> 16) & 0xFF);
            ipParts[2] = (int)((index >> 8) & 0xFF);
            ipParts[3] = (int)(index & 0xFF);
            return String.Join('.',ipParts);
        }
    }
}
