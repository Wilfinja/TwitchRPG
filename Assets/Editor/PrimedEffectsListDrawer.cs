
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(PrimedEffectsListAttribute))]
public class PrimedEffectsListDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        EnsureInstantiated(property);

        float height = EditorGUIUtility.singleLineHeight;

        if (!property.isExpanded)
            return height;

        SerializedProperty endProperty = property.GetEndProperty();
        SerializedProperty child = property.Copy();
        bool enterChildren = true;

        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, endProperty))
        {
            height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
            enterChildren = false;
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EnsureInstantiated(property);

        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            SerializedProperty endProperty = property.GetEndProperty();
            SerializedProperty child = property.Copy();
            bool enterChildren = true;

            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, endProperty))
            {
                float h = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), child, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
                enterChildren = false;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    // A freshly-inserted managed reference starts null — Unity has no built-in way to assign
    // a concrete type from the Inspector for a single-concrete-type SerializeReference field,
    // so we assign it ourselves the moment we see it.
    private static void EnsureInstantiated(SerializedProperty property)
    {
        if (property.propertyType == SerializedPropertyType.ManagedReference && property.managedReferenceValue == null)
        {
            property.managedReferenceValue = new StatusEffect();
        }
    }
}
#endif
