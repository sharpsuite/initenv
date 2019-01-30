using System;
using System.Collections.Generic;
using System.Text;

namespace InitializeEnvironment
{
    public class UnmountAuxiliaryPartitionStage : IStage
    {
        public string StageIdentifier => "unmount-aux-partition";
        
        public UnmountAuxiliaryPartitionStage()
        {

        }

        public bool Execute()
        {
            if (!PrepareAuxiliaryPartitionStage.mounted_part)
                return true; // no need to do anything

            Utilities.RunCommand("umount", PrepareAuxiliaryPartitionStage.temp_mount_point);

            return true;
        }
    }
}
