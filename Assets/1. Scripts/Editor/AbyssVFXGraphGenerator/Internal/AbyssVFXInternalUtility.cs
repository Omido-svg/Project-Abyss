#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor.VFX;
using UnityEngine;

namespace ProjectAbyss.Editor.VFXAI
{
    internal static class AbyssVFXInternalUtility
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        internal static bool TrySetEnumSetting(VFXModel model, string settingName, string enumName, out string warning)
        {
            warning = string.Empty;
            FieldInfo field = FindField(model.GetType(), settingName);
            if (field == null || !field.FieldType.IsEnum)
            {
                warning = $"{model.GetType().Name}.{settingName} enum setting을 찾지 못했습니다.";
                return false;
            }

            try
            {
                object value = Enum.Parse(field.FieldType, enumName, true);
                model.SetSettingValue(settingName, value);
                return true;
            }
            catch (Exception ex)
            {
                warning = $"{settingName}={enumName} 설정 실패: {ex.Message}";
                return false;
            }
        }

        internal static bool TrySetObjectSetting(
            VFXModel model,
            string settingName,
            UnityEngine.Object value,
            out string warning)
        {
            warning = string.Empty;

            if (model == null ||
                string.IsNullOrWhiteSpace(settingName) ||
                value == null)
            {
                warning = "Model, setting name 또는 value가 없습니다.";
                return false;
            }

            FieldInfo field =
                FindField(model.GetType(), settingName);

            if (field != null &&
                field.FieldType.IsInstanceOfType(value))
            {
                try
                {
                    model.SetSettingValue(settingName, value);
                    return true;
                }
                catch (Exception exception)
                {
                    warning =
                        $"{settingName} 설정 실패: {exception.Message}";
                    return false;
                }
            }

            PropertyInfo property =
                FindProperty(model.GetType(), settingName);

            if (property != null &&
                property.CanWrite &&
                property.PropertyType.IsInstanceOfType(value))
            {
                try
                {
                    property.SetValue(model, value);
                    return true;
                }
                catch (Exception exception)
                {
                    warning =
                        $"{settingName} property 설정 실패: {exception.Message}";
                    return false;
                }
            }

            warning =
                $"{model.GetType().Name}.{settingName} Object setting을 찾지 못했습니다.";
            return false;
        }

        // VFX Graph 17.0.4에서 VFXSlotContainerModel은
        // VFXSlotContainerModel<ParentType, ChildrenType> 제네릭 타입이다.
        // 구체 타입 인자를 컴파일 타임에 고정하지 않고 실제 모델 타입을 Reflection으로 조회한다.
        internal static bool TryAssignInputSlotByName(VFXModel model, string slotName, object value)
        {
            if (model == null || string.IsNullOrWhiteSpace(slotName))
                return false;

            if (!TryGetInputSlots(model, out IEnumerable slots))
                return false;

            foreach (object rawSlot in slots)
            {
                if (rawSlot is VFXSlot slot && TryAssignRecursive(slot, slotName, value))
                    return true;
            }

            return false;
        }

        internal static bool TryAssignInputSlotByIndex(VFXModel model, int index, object value)
        {
            if (model == null || index < 0)
                return false;

            try
            {
                MethodInfo getInputSlot = FindMethod(model.GetType(), "GetInputSlot", typeof(int));
                if (getInputSlot?.Invoke(model, new object[] { index }) is VFXSlot slot)
                    return TryAssignSlotValue(slot, value);
            }
            catch
            {
                // 아래의 inputSlots 열거 fallback을 사용한다.
            }

            if (!TryGetInputSlots(model, out IEnumerable slots))
                return false;

            int currentIndex = 0;
            foreach (object rawSlot in slots)
            {
                if (rawSlot is not VFXSlot slot)
                    continue;

                if (currentIndex == index)
                    return TryAssignSlotValue(slot, value);

                currentIndex++;
            }

            return false;
        }

        internal static string DescribeInputSlot(VFXModel model, int index)
        {
            if (model == null)
                return "<null model>";

            try
            {
                MethodInfo getInputSlot = FindMethod(model.GetType(), "GetInputSlot", typeof(int));
                if (getInputSlot?.Invoke(model, new object[] { index }) is VFXSlot slot)
                {
                    object current = SafeGetSlotValue(slot);
                    Type expected = current?.GetType() ?? TryGetSlotDeclaredType(slot);
                    return $"{slot.name} ({slot.GetType().FullName}, value={expected?.FullName ?? "unknown"})";
                }
            }
            catch
            {
                // 진단 문자열만 반환하므로 예외를 외부로 전파하지 않는다.
            }

            return $"index {index} (unresolved)";
        }

        private static bool TryGetInputSlots(VFXModel model, out IEnumerable slots)
        {
            slots = null;

            PropertyInfo property = FindProperty(model.GetType(), "inputSlots");
            if (property?.GetValue(model) is IEnumerable propertySlots)
            {
                slots = propertySlots;
                return true;
            }

            FieldInfo field = FindField(model.GetType(), "inputSlots");
            if (field?.GetValue(model) is IEnumerable fieldSlots)
            {
                slots = fieldSlots;
                return true;
            }

            return false;
        }

        private static bool TryAssignRecursive(VFXSlot slot, string slotName, object value)
        {
            if (string.Equals(slot.name, slotName, StringComparison.OrdinalIgnoreCase))
                return TryAssignSlotValue(slot, value);

            PropertyInfo childrenProperty = FindProperty(slot.GetType(), "children");
            IEnumerable children = childrenProperty?.GetValue(slot) as IEnumerable;

            if (children == null)
            {
                FieldInfo childrenField = FindField(slot.GetType(), "children");
                children = childrenField?.GetValue(slot) as IEnumerable;
            }

            if (children == null)
                return false;

            foreach (object child in children)
            {
                if (child is VFXSlot childSlot && TryAssignRecursive(childSlot, slotName, value))
                    return true;
            }

            return false;
        }

        private static bool TryAssignSlotValue(VFXSlot slot, object sourceValue)
        {
            if (slot == null)
                return false;

            try
            {
                object currentValue = SafeGetSlotValue(slot);
                Type expectedType = currentValue?.GetType() ?? TryGetSlotDeclaredType(slot);

                if (!TryConvertValue(sourceValue, expectedType, currentValue, out object convertedValue))
                    return false;

                slot.value = convertedValue;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[Abyss VFX] Slot 값 설정 실패: {slot.name} / {slot.GetType().FullName}\n" +
                    $"입력={sourceValue?.GetType().FullName ?? "null"}\n{ex.Message}");
                return false;
            }
        }

        private static object SafeGetSlotValue(VFXSlot slot)
        {
            try
            {
                return slot.value;
            }
            catch
            {
                return null;
            }
        }

        private static Type TryGetSlotDeclaredType(VFXSlot slot)
        {
            try
            {
                PropertyInfo propertyInfo = FindProperty(slot.GetType(), "property");
                object property = propertyInfo?.GetValue(slot);
                if (property != null)
                {
                    PropertyInfo typeProperty = FindProperty(property.GetType(), "type");
                    if (typeProperty?.GetValue(property) is Type propertyType)
                        return propertyType;

                    FieldInfo typeField = FindField(property.GetType(), "type");
                    if (typeField?.GetValue(property) is Type fieldType)
                        return fieldType;
                }
            }
            catch
            {
                // 선언 타입을 못 찾으면 현재 값 타입 또는 직접 대입을 사용한다.
            }

            return null;
        }

        private static bool TryConvertValue(object sourceValue, Type expectedType, object currentValue, out object convertedValue)
        {
            convertedValue = sourceValue;

            if (expectedType == null)
                return true;

            if (sourceValue == null)
            {
                if (!expectedType.IsValueType || Nullable.GetUnderlyingType(expectedType) != null)
                    return true;

                convertedValue = Activator.CreateInstance(expectedType);
                return true;
            }

            if (expectedType.IsInstanceOfType(sourceValue))
                return true;

            if (expectedType == typeof(Vector3) && TryExtractVector3(sourceValue, out Vector3 vector3))
            {
                convertedValue = vector3;
                return true;
            }

            if (expectedType == typeof(Vector4))
            {
                if (sourceValue is Color color4)
                {
                    convertedValue = new Vector4(color4.r, color4.g, color4.b, color4.a);
                    return true;
                }

                if (TryExtractVector3(sourceValue, out Vector3 vectorFor4))
                {
                    convertedValue = new Vector4(vectorFor4.x, vectorFor4.y, vectorFor4.z, 0f);
                    return true;
                }
            }

            if (expectedType == typeof(Color))
            {
                if (sourceValue is Vector4 vectorColor4)
                {
                    convertedValue = new Color(vectorColor4.x, vectorColor4.y, vectorColor4.z, vectorColor4.w);
                    return true;
                }

                if (TryExtractVector3(sourceValue, out Vector3 vectorColor3))
                {
                    convertedValue = new Color(vectorColor3.x, vectorColor3.y, vectorColor3.z, 1f);
                    return true;
                }
            }

            string expectedName = expectedType.FullName ?? expectedType.Name;
            if ((expectedName == "UnityEditor.VFX.Position" || expectedName == "UnityEditor.VFX.Vector") &&
                TryExtractVector3(sourceValue, out Vector3 spaceableVector))
            {
                if (TryCreateSpaceableValue(expectedType, currentValue, spaceableVector, out convertedValue))
                    return true;
            }

            if (expectedType.IsEnum)
            {
                try
                {
                    convertedValue = sourceValue is string enumText
                        ? Enum.Parse(expectedType, enumText, true)
                        : Enum.ToObject(expectedType, sourceValue);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            try
            {
                convertedValue = Convert.ChangeType(sourceValue, expectedType);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryCreateSpaceableValue(Type expectedType, object currentValue, Vector3 value, out object result)
        {
            result = null;

            // 현재 기본값을 복제해 Coordinate Space 등 부가 정보를 보존한다.
            object boxed = currentValue ?? Activator.CreateInstance(expectedType);
            if (TrySetVector3Member(boxed, expectedType, value))
            {
                result = boxed;
                return true;
            }

            ConstructorInfo constructor = expectedType.GetConstructor(InstanceFlags, null, new[] { typeof(Vector3) }, null);
            if (constructor != null)
            {
                result = constructor.Invoke(new object[] { value });
                return true;
            }

            foreach (MethodInfo method in expectedType.GetMethods(StaticFlags))
            {
                if ((method.Name != "op_Implicit" && method.Name != "op_Explicit") || method.ReturnType != expectedType)
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Vector3))
                {
                    result = method.Invoke(null, new object[] { value });
                    return true;
                }
            }

            return false;
        }

        private static bool TrySetVector3Member(object boxed, Type type, Vector3 value)
        {
            string[] preferredNames =
            {
                "position", "Position", "vector", "Vector", "value", "Value",
                "m_Position", "m_Vector", "m_Value"
            };

            foreach (string name in preferredNames)
            {
                FieldInfo field = FindField(type, name);
                if (field != null && field.FieldType == typeof(Vector3))
                {
                    field.SetValue(boxed, value);
                    return true;
                }

                PropertyInfo property = FindProperty(type, name);
                if (property != null && property.PropertyType == typeof(Vector3) && property.CanWrite)
                {
                    property.SetValue(boxed, value);
                    return true;
                }
            }

            for (Type current = type; current != null; current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(InstanceFlags))
                {
                    if (field.FieldType == typeof(Vector3))
                    {
                        field.SetValue(boxed, value);
                        return true;
                    }
                }

                foreach (PropertyInfo property in current.GetProperties(InstanceFlags))
                {
                    if (property.PropertyType == typeof(Vector3) && property.CanWrite && property.GetIndexParameters().Length == 0)
                    {
                        property.SetValue(boxed, value);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryExtractVector3(object value, out Vector3 result)
        {
            switch (value)
            {
                case Vector3 vector3:
                    result = vector3;
                    return true;
                case Vector4 vector4:
                    result = new Vector3(vector4.x, vector4.y, vector4.z);
                    return true;
                case Color color:
                    result = new Vector3(color.r, color.g, color.b);
                    return true;
            }

            if (value != null)
            {
                Type type = value.GetType();
                string[] names = { "position", "Position", "vector", "Vector", "value", "Value", "m_Position", "m_Vector", "m_Value" };
                foreach (string name in names)
                {
                    FieldInfo field = FindField(type, name);
                    if (field != null && field.FieldType == typeof(Vector3))
                    {
                        result = (Vector3)field.GetValue(value);
                        return true;
                    }

                    PropertyInfo property = FindProperty(type, name);
                    if (property != null && property.PropertyType == typeof(Vector3) && property.CanRead)
                    {
                        result = (Vector3)property.GetValue(value);
                        return true;
                    }
                }
            }

            result = default;
            return false;
        }

        internal static FieldInfo FindField(Type type, string fieldName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(fieldName, InstanceFlags);
                if (field != null)
                    return field;
            }

            return null;
        }

        internal static PropertyInfo FindProperty(Type type, string propertyName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(propertyName, InstanceFlags);
                if (property != null)
                    return property;
            }

            return null;
        }

        internal static MethodInfo FindMethod(Type type, string methodName, params Type[] parameterTypes)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                MethodInfo method = current.GetMethod(methodName, InstanceFlags, null, parameterTypes, null);
                if (method != null)
                    return method;
            }

            return null;
        }
    }
}
#endif