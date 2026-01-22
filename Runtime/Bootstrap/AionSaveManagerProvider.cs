// com.bpg.aion/Runtime/Bootstrap/AionSaveManagerProvider.cs
#nullable enable
using System;

namespace BPG.Aion
{
    /// <summary>
    /// Singleton accessor for <see cref="SaveManager"/> for games that don't use dependency injection.
    /// The manager is lazily created and configured via project settings on first access.
    /// </summary>
    public static class AionSaveManagerProvider
    {
        private static readonly object _lock = new();
        private static SaveManager? _instance;
        private static AionSaveManagerFactoryOptions? _pendingOptions;
        private static bool _configured;

        /// <summary>
        /// Gets the singleton SaveManager instance, creating it on first access.
        /// </summary>
        /// <remarks>
        /// Call <see cref="Configure"/> before first access if you need to override components.
        /// </remarks>
        public static SaveManager Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                lock (_lock)
                {
                    if (_instance != null)
                        return _instance;

                    _instance = AionSaveManagerFactory.Create(_pendingOptions);
                    _configured = true;
                }

                return _instance;
            }
        }

        /// <summary>
        /// Pre-configures the factory options before the instance is created.
        /// Must be called before the first access to <see cref="Instance"/>.
        /// </summary>
        /// <param name="options">Factory options with component overrides.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if called after the instance has already been created.
        /// </exception>
        public static void Configure(AionSaveManagerFactoryOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            lock (_lock)
            {
                if (_configured)
                {
                    throw new InvalidOperationException(
                        "AionSaveManagerProvider.Configure must be called before first access to Instance. " +
                        "Call ResetForTests() first if you need to reconfigure.");
                }

                _pendingOptions = options;
            }
        }

        /// <summary>
        /// Resets the singleton instance. Intended for test cleanup.
        /// </summary>
        /// <remarks>
        /// After calling this, the next access to <see cref="Instance"/> will create a new manager.
        /// </remarks>
        public static void ResetForTests()
        {
            lock (_lock)
            {
                _instance = null;
                _pendingOptions = null;
                _configured = false;
            }
        }
    }
}
