using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusAIO.Core.DataBase.Classes
{
    public enum DatabaseWorkingStatus
    {
        Reading,
        Read,
        Sending,
        Sent,
        Writing,
        Written,
        Still,
        ConnectionProblem,
        NotExisting,
        UpdateProblem,
    }
}

