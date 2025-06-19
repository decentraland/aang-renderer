using System;
using System.Linq;

namespace Data
{
    [Serializable]
    public class ActiveEntitiesRequest
    {
        public string[] pointers;

        public ActiveEntitiesRequest(string[] pointers)
        {
            // Sanitize pointers
            for (var i = 0; i < pointers.Length; i++)
            {
                var originalPointer = pointers[i];
                pointers[i] = originalPointer.Count(c => c == ':') == 6
                    ? originalPointer.Remove(originalPointer.LastIndexOf(':'))
                    : originalPointer;
            }

            this.pointers = pointers;
        }
    }
}