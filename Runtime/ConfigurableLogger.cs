using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Myna.DebugUtilities
{
    using Object = UnityEngine.Object;

    [Serializable]
    public partial class ConfigurableLogger : ILogger, ISerializationCallbackReceiver
    {
        private const string DefaultTag = "Logger";
        private const string NoTagFormat = "{0}: {1}";
        private const string TagFormat = "{0}.{1}: {2}";

        [SerializeField]
        private string _mainTag;

        [SerializeField]
        private string _logLevelPrefsKey;

        [SerializeField]
        private int _defaultLogLevel;

        private string _tag;

        private readonly Stack<string> _tags = new();

        public ILogHandler logHandler { get; set; } = Debug.unityLogger;
        public bool logEnabled { get; set; } = true;
        public LogType filterLogType
        {
            get => (LogType)PlayerPrefs.GetInt(_logLevelPrefsKey, _defaultLogLevel);
            set => PlayerPrefs.SetInt(_logLevelPrefsKey, (int)value);
        }

        public ConfigurableLogger(string mainTag, LogType defaultLogLevel = LogType.Error)
        {
            _mainTag = !string.IsNullOrEmpty(mainTag) ? mainTag : DefaultTag;
            _defaultLogLevel = (int)defaultLogLevel;
            _tag = mainTag;

            RefreshPrefsKey();
        }

        public void PushTag(string tag)
        {
            if (!string.IsNullOrEmpty(tag))
            {
                _tags.Push(tag);
                RefreshTag();
            }
        }

        public void PopTag()
        {
            if (_tags.TryPop(out _))
            {
                RefreshTag();
            }
        }

        public IDisposable OpenTagScope(string tag)
        {
            return TagScope.Use(this, tag);
        }

        #region ILogger implementation
        public bool IsLogTypeAllowed(LogType logType)
        {
            return logEnabled && (logType == LogType.Exception || logType <= filterLogType);
        }

        private static string GetString(object message)
        {
            if (message == null)
            {
                return "Null";
            }

            if (message is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return message.ToString();
        }

        public void Log(LogType logType, object message)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, null, NoTagFormat, _tag, GetString(message));
            }
        }

        public void Log(LogType logType, object message, Object context)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, context, NoTagFormat, _tag, GetString(message));
            }
        }

        public void Log(LogType logType, string tag, object message)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, null, TagFormat, _tag, tag, GetString(message));
            }
        }

        public void Log(LogType logType, string tag, object message, Object context)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, context, TagFormat, _tag, tag, GetString(message));
            }
        }

        public void Log(object message)
        {
            if (IsLogTypeAllowed(LogType.Log))
            {
                logHandler.LogFormat(LogType.Log, null, NoTagFormat, _tag, GetString(message));
            }
        }

        public void Log(string tag, object message)
        {
            if (IsLogTypeAllowed(LogType.Log))
            {
                logHandler.LogFormat(LogType.Log, null, TagFormat, _tag, tag, GetString(message));
            }
        }

        public void Log(string tag, object message, Object context)
        {
            if (IsLogTypeAllowed(LogType.Log))
            {
                logHandler.LogFormat(LogType.Log, context, TagFormat, _tag, tag, GetString(message));
            }
        }

        public void LogWarning(object message)
        {
            if (IsLogTypeAllowed(LogType.Warning))
            {
                logHandler.LogFormat(LogType.Warning, null, NoTagFormat, _tag, GetString(message));
            }
        }

        public void LogWarning(string tag, object message)
        {
            if (IsLogTypeAllowed(LogType.Warning))
            {
                logHandler.LogFormat(LogType.Warning, null, TagFormat, _tag, tag, GetString(message));
            }
        }

        public void LogWarning(string tag, object message, Object context)
        {
            if (IsLogTypeAllowed(LogType.Warning))
            {
                logHandler.LogFormat(LogType.Warning, context, TagFormat, _tag, tag, GetString(message));
            }
        }

        public void LogError(object message)
        {
            if (IsLogTypeAllowed(LogType.Warning))
            {
                logHandler.LogFormat(LogType.Error, null, NoTagFormat, _tag, GetString(message));
            }
        }

        public void LogError(string tag, object message)
        {
            if (IsLogTypeAllowed(LogType.Error))
            {
                logHandler.LogFormat(LogType.Error, null, TagFormat, _tag, tag, GetString(message));
            }
        }

        public void LogError(string tag, object message, Object context)
        {
            if (IsLogTypeAllowed(LogType.Error))
            {
                logHandler.LogFormat(LogType.Error, context, TagFormat, _tag, tag, GetString(message));
            }
        }

        public void LogException(Exception exception)
        {
            if (logEnabled)
            {
                logHandler.LogException(exception, null);
            }
        }

        public void LogException(Exception exception, Object context)
        {
            if (logEnabled)
            {
                logHandler.LogException(exception, context);
            }
        }

        public void LogFormat(LogType logType, string format, params object[] args)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, null, format, args);
            }
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, context, format, args);
            }
        }
        #endregion ILogger implementation

        #region ISerializationCallbackReceiver implementation
        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            _tag = _mainTag;
            RefreshPrefsKey();
        }
        #endregion ISerializationCallbackReceiver implementation

        private void RefreshTag()
        {
            var sb = new StringBuilder();
            sb.Append(_mainTag);
            foreach (var tag in _tags)
            {
                sb.Append('.');
                sb.Append(tag);
            }
            _tag = sb.ToString();
        }

        private void RefreshPrefsKey()
        {
            _logLevelPrefsKey = string.Concat(nameof(ConfigurableLogger), "_", _mainTag, "_LogLevel");
        }

        private class TagScope : IDisposable
        {
            private ConfigurableLogger Logger { get; set; }
            private bool Disposed { get; set; }

            private static readonly Stack<TagScope> _pool = new();

            public static TagScope Use(ConfigurableLogger logger, string tag)
            {
                if (!_pool.TryPop(out var scope))
                {
                    scope = new TagScope();
                }

                scope.Logger = logger;
                scope.Disposed = false;

                logger.PushTag(tag);
                return scope;
            }

            public void Dispose()
            {
                if (!Disposed)
                {
                    Logger.PopTag();
                    Logger = null;

                    _pool.Push(this);
                }
            }
        }
    }

#if UNITY_EDITOR
    partial class ConfigurableLogger
    {
#pragma warning disable 0414
        [SerializeField]
        private LogType _inspectorLogLevel = 0;
#pragma warning restore 0414

        public void DrawGuiLayout(string label) => DrawGuiLayout(EditorGUIUtility.TrTempContent(label));
        public void DrawGuiLayout(GUIContent label)
        {
            filterLogType = (LogType)EditorGUILayout.EnumPopup(label, filterLogType);
        }
    }

    [CustomPropertyDrawer(typeof(ConfigurableLogger))]
    public class ConfigurableLoggerPropertyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var target = (ConfigurableLogger)property.boxedValue;
            var inspectorLogLevel = property.FindPropertyRelative("_inspectorLogLevel");
            inspectorLogLevel.intValue = (int)target.filterLogType;

            using var changeCheck = new EditorGUI.ChangeCheckScope();
            EditorGUI.PropertyField(position, inspectorLogLevel, label);

            if (changeCheck.changed)
            {
                target.filterLogType = (LogType)inspectorLogLevel.intValue;
            }

            inspectorLogLevel.intValue = 0;
        }
    }
#endif
}
