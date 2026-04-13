using System;
using System.Collections.Generic;
using System.Text;

namespace EmpireIdle.Domain.ValueObjects
{
    /// <summary>
    /// Типи ресурсів у грі. Розширюється через GameConfig без зміни коду.
    /// </summary>
    public class ResourceType
    {
        public const string Gold = "gold";
        public const string Wood = "wood";
        public const string Metal = "metal";
        public const string Stone = "stone";
    }
}
