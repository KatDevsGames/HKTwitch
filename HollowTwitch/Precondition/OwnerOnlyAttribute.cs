using System;
using HollowTwitch.Entities.Attributes;

namespace HollowTwitch.Precondition
{
    public class OwnerOnlyAttribute : PreconditionAttribute
    {
        public override bool Check(string user)
        {
            return string.Equals(user, CrowdControl.Instance.Config.TwitchChannel, StringComparison.OrdinalIgnoreCase);
        }
    }
}