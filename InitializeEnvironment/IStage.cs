using System;
using System.Collections.Generic;
using System.Text;

namespace InitializeEnvironment
{
    public interface IStage
    {
        string StageIdentifier { get; }

        bool Execute();
    }
}
