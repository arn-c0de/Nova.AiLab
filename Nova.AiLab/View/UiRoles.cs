using System;
using System.Text;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>
    /// The role names both pages print, generated from <see cref="UnitRole"/>
    /// rather than typed out beside it.
    /// <para>
    /// IT WAS TYPED OUT TWICE AND ONE COPY WAS WRONG. The live panel carried its
    /// own map of numbers to names — <c>12:'tank'</c>, <c>13:'artillery'</c> —
    /// while role 12 is <c>BasicInfantry</c> and 13 is <c>AntiArmorInfantry</c>.
    /// Every rifleman in that panel was labelled a tank. Nothing went red,
    /// because a name table has nothing to disagree with: it is only wrong
    /// against an enum nobody asked.
    /// </para>
    /// <para>
    /// So the enum is asked. Position in the array IS the role number, the same
    /// contract <c>GOAL_NAMES</c> holds to, and a role added in
    /// <c>Simulation/State/</c> shows up in both pages without either of them
    /// being edited.
    /// </para>
    /// </summary>
    internal static class UiRoles
    {
        /// <summary>
        /// The names as a JavaScript array literal, index = role number.
        /// <para>
        /// Camel case, except for a name that is already an abbreviation —
        /// <c>HQ</c> stays <c>HQ</c> rather than becoming <c>hQ</c>, which is
        /// what the player page has always printed.
        /// </para>
        /// </summary>
        public static string JsArray()
        {
            // Indexed by VALUE, not by the order GetNames happens to return: a
            // gap in the enum would otherwise shift every name after it by one,
            // which is exactly the failure this class exists to end.
            var roles = (UnitRole[])Enum.GetValues(typeof(UnitRole));
            int highest = 0;
            foreach (UnitRole role in roles)
            {
                if ((int)role > highest) highest = (int)role;
            }

            var names = new string[highest + 1];
            foreach (UnitRole role in roles) names[(int)role] = CamelCase(role.ToString());

            var array = new StringBuilder(16 * names.Length);
            array.Append('[');
            for (int i = 0; i < names.Length; i++)
            {
                if (i > 0) array.Append(',');
                array.Append('\'').Append(names[i] ?? "unknown").Append('\'');
            }
            return array.Append(']').ToString();
        }

        private static string CamelCase(string name)
        {
            if (name.Length == 0) return name;
            if (name.ToUpperInvariant() == name) return name; // HQ
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
