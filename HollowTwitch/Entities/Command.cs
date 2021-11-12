using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HollowTwitch.Entities.Attributes;

namespace HollowTwitch.Entities
{
    public class Command
    {
        public string Name { get; }
        public MethodInfo MethodInfo { get; }
        public int Priority { get; }
        public ParameterInfo[] Parameters { get; }
        public object ClassInstance { get; }
        public TimeSpan? Cooldown { get; }
        public IEnumerable<PreconditionAttribute> Preconditions { get; }


        public Command(string name, MethodInfo method, object classInstance, TimeSpan? cooldown)
        {
            Name = name;
            MethodInfo = method;
            Parameters = MethodInfo.GetParameters();
            Priority = Parameters.Length;
            ClassInstance = classInstance;
            Cooldown = cooldown;
            Preconditions = method.GetCustomAttributes(typeof(PreconditionAttribute), false).Cast<PreconditionAttribute>();
        }

    }
}
