using System;
using Microsoft.Build.Utilities;

namespace AnalyzeDotNetProject
{
    public class EnvironmentVariableWriterTask : Task
    {
        public EnvironmentVariableWriterTask()
        {
        }

        public override bool Execute()
        {
            Log.LogMessage("About to print all environt variables in MSBuils scopre");
            foreach(string envVarName in Environment.GetEnvironmentVariables().Keys)
            { 
				Log.LogMessage($"{envVarName}: {Environment.GetEnvironmentVariable(envVarName)}");
			}
            return true;
        }
    }
}
