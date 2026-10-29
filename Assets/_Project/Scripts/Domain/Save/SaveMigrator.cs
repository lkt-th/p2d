using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    /// <summary>
    /// Upgrades older saves one version at a time. When the save format changes:
    /// bump <see cref="CurrentVersion"/> and register a step from the previous version.
    /// Fields added later get their C# default on load, so a step only fixes values.
    /// </summary>
    public sealed class SaveMigrator
    {
        public const int CurrentVersion = 2;

        readonly Dictionary<int, Action<PlayerSave>> steps = new Dictionary<int, Action<PlayerSave>>();
        readonly int targetVersion;

        public int TargetVersion => targetVersion;

        public SaveMigrator(int targetVersion = CurrentVersion)
        {
            this.targetVersion = targetVersion;
        }

        /// <summary>Registers the step that upgrades a save from <paramref name="fromVersion"/> to the next.</summary>
        public SaveMigrator Add(int fromVersion, Action<PlayerSave> step)
        {
            steps[fromVersion] = step;
            return this;
        }

        /// <summary>Returns true when the save was changed.</summary>
        public bool Migrate(PlayerSave save)
        {
            if (save.version > targetVersion)
                throw new InvalidOperationException(
                    "Save version " + save.version + " is newer than this build (" + targetVersion + "). Update the app.");

            bool changed = false;
            while (save.version < targetVersion)
            {
                Action<PlayerSave> step;
                if (!steps.TryGetValue(save.version, out step))
                    throw new InvalidOperationException("No migration from save version " + save.version);
                step(save);
                save.version++;
                changed = true;
            }
            return changed;
        }

        public static SaveMigrator Default() => new SaveMigrator()
            // v2 added the egg: saves made before it already have a dragon.
            .Add(1, save => save.dragon.hatched = true);
    }
}
