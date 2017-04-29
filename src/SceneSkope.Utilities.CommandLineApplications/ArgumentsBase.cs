using System;
using System.Collections.Generic;
using System.Text;

namespace SceneSkope.Utilities.CommandLineApplications
{
    public abstract class ArgumentsBase
    {
        public abstract string Key { get; set; }

        public abstract string SeqHost { get; set; }

        public abstract string SeqToken { get; set; }

        public abstract string LockFile { get; set; }

        public abstract bool Help { get; set; }

        public abstract bool NoConsole { get; set; }
    }
}
