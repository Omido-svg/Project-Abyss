using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class SkillCameraDefinitionValidator
{
    public static bool Validate(
        SkillCameraDefinition definition,
        UnityEngine.Object context = null,
        bool logWarnings = true)
    {
        if (definition == null)
            return true;

        object shots = ReadNamedMember(definition, "Shots");

        if (shots is not IEnumerable enumerable)
            return true;

        bool valid = true;
        int index = 0;

        foreach (object shot in enumerable)
        {
            if (shot == null)
            {
                index++;
                continue;
            }

            string typeText =
                ReadNamedMember(shot, "ShotType")?.ToString() ??
                ReadNamedMember(shot, "Type")?.ToString() ??
                string.Empty;

            bool requiresCameraPoint =
                typeText.IndexOf("CameraPoint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeText.IndexOf("ScenePoint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeText.IndexOf("SceneCamera", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!requiresCameraPoint)
            {
                index++;
                continue;
            }

            List<MemberValue> candidates =
                FindCameraPointMembers(shot);

            bool hasValidValue = false;

            foreach (MemberValue candidate in candidates)
            {
                if (!IsMissing(candidate.Value))
                {
                    hasValidValue = true;
                    break;
                }
            }

            if (!hasValidValue)
            {
                valid = false;

                if (logWarnings)
                {
                    string memberNames = candidates.Count == 0
                        ? "<CameraPoint member not found>"
                        : string.Join(", ", candidates.ConvertAll(x => x.Name));

                    Debug.LogWarning(
                        $"[CAMERA VALIDATION] CameraPoint 키/참조 누락 / " +
                        $"Definition={definition.name}, ShotIndex={index}, " +
                        $"Type={typeText}, Members={memberNames}",
                        context != null ? context : definition);
                }
            }

            index++;
        }

        return valid;
    }

    private static List<MemberValue> FindCameraPointMembers(object target)
    {
        List<MemberValue> result = new List<MemberValue>();

        if (target == null)
            return result;

        Type type = target.GetType();
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (!LooksLikeCameraPointMember(field.Name))
                continue;

            result.Add(new MemberValue(field.Name, field.GetValue(target)));
        }

        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            if (!property.CanRead ||
                property.GetIndexParameters().Length > 0 ||
                !LooksLikeCameraPointMember(property.Name))
            {
                continue;
            }

            object value = null;

            try
            {
                value = property.GetValue(target);
            }
            catch
            {
                // Validation must never break battle startup.
            }

            result.Add(new MemberValue(property.Name, value));
        }

        return result;
    }

    private static bool LooksLikeCameraPointMember(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        return
            name.IndexOf("CameraPoint", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("ScenePoint", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("PointKey", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsMissing(object value)
    {
        if (value == null)
            return true;

        if (value is string text)
            return string.IsNullOrWhiteSpace(text);

        if (value is UnityEngine.Object unityObject)
            return unityObject == null;

        return false;
    }

    private static object ReadNamedMember(object target, string name)
    {
        if (target == null)
            return null;

        Type type = target.GetType();
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        FieldInfo field = type.GetField(name, flags);

        if (field != null)
            return field.GetValue(target);

        PropertyInfo property = type.GetProperty(name, flags);

        if (property == null ||
            !property.CanRead ||
            property.GetIndexParameters().Length > 0)
        {
            return null;
        }

        try
        {
            return property.GetValue(target);
        }
        catch
        {
            return null;
        }
    }

    private readonly struct MemberValue
    {
        public MemberValue(string name, object value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public object Value { get; }
    }
}
