using Cake.Core;
using Cake.Frosting;

namespace Build.Tasks.Common;

public class CleanTask : FrostingContext
{
    public CleanTask(ICakeContext context) : base(context)
    {
    }
}
