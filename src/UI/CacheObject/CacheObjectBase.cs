﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityExplorer.Core.Runtime;
using UnityExplorer.UI.CacheObject.IValues;
using UnityExplorer.UI.CacheObject.Views;
using UnityExplorer.UI.Models;

namespace UnityExplorer.UI.CacheObject
{
    public enum ValueState
    {
        NotEvaluated,
        Exception,
        Boolean,
        Number,
        String,
        Enum,
        Collection,
        Dictionary,
        ValueStruct,
        Color,
        Unsupported
    }

    public abstract class CacheObjectBase
    {
        public ICacheObjectController Owner { get; set; }

        public CacheObjectCell CellView { get; internal set; }

        public object Value { get; protected set; }
        public Type FallbackType { get; protected set; }
        public bool LastValueWasNull { get; private set; }

        public ValueState State = ValueState.NotEvaluated;
        public Type LastValueType;

        public InteractiveValue IValue { get; private set; }
        public Type CurrentIValueType { get; private set; }
        public bool SubContentShowWanted { get; private set; }

        public string NameLabelText { get; protected set; }
        public string ValueLabelText { get; protected set; }

        public abstract bool ShouldAutoEvaluate { get; }
        public abstract bool HasArguments { get; }
        public abstract bool CanWrite { get; }
        public bool HadException { get; protected set; }
        public Exception LastException { get; protected set; }

        public virtual void SetFallbackType(Type fallbackType)
        {
            this.FallbackType = fallbackType;
            this.ValueLabelText = GetValueLabel();
        }

        protected const string NOT_YET_EVAL = "<color=grey>Not yet evaluated</color>";

        public virtual void ReleasePooledObjects()
        {
            if (this.IValue != null)
                ReleaseIValue();

            if (this.CellView != null)
                UnlinkFromView();
        }

        public virtual void SetView(CacheObjectCell cellView)
        {
            this.CellView = cellView;
            cellView.Occupant = this;
        }

        public virtual void UnlinkFromView()
        {
            if (this.CellView == null)
                return;

            this.CellView.Occupant = null;
            this.CellView = null;

            if (this.IValue != null)
                this.IValue.UIRoot.transform.SetParent(InactiveIValueHolder.transform, false);
        }

        // Updating and applying values

        public void SetUserValue(object value)
        {
            value = value.TryCast(FallbackType);

            TrySetUserValue(value);

            if (CellView != null)
                SetDataToCell(CellView);

            // If the owner's parent CacheObject is set, we are setting the value of an inspected struct.
            // Set the inspector target as the value back to that parent cacheobject.
            if (Owner.ParentCacheObject != null)
                Owner.ParentCacheObject.SetUserValue(Owner.Target);
        }

        public abstract void TrySetUserValue(object value);

        // The only method which sets the CacheObjectBase.Value
        public virtual void SetValueFromSource(object value)
        {
            this.Value = value;

            if (!Value.IsNullOrDestroyed())
                Value = Value.TryCast();

            ProcessOnEvaluate();

            if (this.IValue != null)
            {
                if (SubContentShowWanted)
                    this.IValue.SetValue(Value);
                else
                    IValue.PendingValueWanted = true;
            }
        }

        protected virtual void ProcessOnEvaluate()
        {
            var prevState = State;

            if (HadException)
            {
                LastValueWasNull = true;
                LastValueType = FallbackType;
                State = ValueState.Exception;
            }
            else if (Value.IsNullOrDestroyed())
            {
                LastValueWasNull = true;
                State = GetStateForType(FallbackType);
            }
            else
            {
                LastValueWasNull = false;
                State = GetStateForType(Value.GetActualType());
            }

            if (IValue != null)
            {
                // If we changed states (always needs IValue change)
                // or if the value is null, and the fallback type isnt string (we always want to edit strings).
                if (State != prevState || (State != ValueState.String && Value.IsNullOrDestroyed()))
                {
                    // need to return IValue
                    ReleaseIValue();
                    SubContentShowWanted = false;
                }
            }

            // Set label text
            this.ValueLabelText = GetValueLabel();
        }

        public ValueState GetStateForType(Type type)
        {
            if (LastValueType == type)
                return State;

            LastValueType = type;
            if (type == typeof(bool))
                return ValueState.Boolean;
            else if (type.IsPrimitive || type == typeof(decimal))
                return ValueState.Number;
            else if (type == typeof(string))
                return ValueState.String;
            else if (type.IsEnum)
                return ValueState.Enum;
            else if (type == typeof(Color) || type == typeof(Color32))
                return ValueState.Color;
            else if (InteractiveValueStruct.SupportsType(type))
                return ValueState.ValueStruct;
            else if (ReflectionUtility.IsDictionary(type))
                return ValueState.Dictionary;
            else if (!typeof(Transform).IsAssignableFrom(type) && ReflectionUtility.IsEnumerable(type))
                return ValueState.Collection;
            else
                return ValueState.Unsupported;
        }

        protected string GetValueLabel()
        {
            string label = "";

            switch (State)
            {
                case ValueState.NotEvaluated:
                    return $"<i>{NOT_YET_EVAL} ({SignatureHighlighter.Parse(FallbackType, true)})</i>";

                case ValueState.Exception:
                    return $"<i><color=red>{LastException.ReflectionExToString()}</color></i>";

                // bool and number dont want the label for the value at all
                case ValueState.Boolean:
                case ValueState.Number:
                    return null;

                // and valuestruct also doesnt want it if we can parse it
                case ValueState.ValueStruct:
                    if (ParseUtility.CanParse(LastValueType))
                        return null;
                    break;

                // string wants it trimmed to max 200 chars
                case ValueState.String:
                    if (!LastValueWasNull)
                    {
                        string s = Value as string;
                        return $"\"{ToStringUtility.PruneString(s, 200, 5)}\"";
                    }
                    break;

                // try to prefix the count of the collection for lists and dicts
                case ValueState.Collection:
                    if (!LastValueWasNull)
                    {
                        if (Value is IList iList)
                            label = $"[{iList.Count}] ";
                        else if (Value is ICollection iCol)
                            label = $"[{iCol.Count}] ";
                        else
                            label = "[?] ";
                    }
                    break;

                case ValueState.Dictionary:
                    if (!LastValueWasNull)
                    {
                        if (Value is IDictionary iDict)
                            label = $"[{iDict.Count}] ";
                        else
                            label = "[?] ";
                    }
                    break;
            }

            // Cases which dont return will append to ToStringWithType

            return label += ToStringUtility.ToStringWithType(Value, FallbackType, true);
        }

        // Setting cell state from our model

        /// <summary>Return true if SetCell should abort, false if it should continue.</summary>
        protected abstract bool SetCellEvaluateState(CacheObjectCell cell);

        public virtual void SetDataToCell(CacheObjectCell cell)
        {
            cell.NameLabel.text = NameLabelText;
            cell.ValueLabel.gameObject.SetActive(true);

            cell.SubContentHolder.gameObject.SetActive(SubContentShowWanted);
            if (IValue != null)
            {
                IValue.UIRoot.transform.SetParent(cell.SubContentHolder.transform, false);
                IValue.SetLayout();
            }

            if (SetCellEvaluateState(cell))
                return;

            switch (State)
            {
                case ValueState.Exception:
                    SetValueState(cell, ValueStateArgs.Default);
                    break;
                case ValueState.Boolean:
                    SetValueState(cell, new ValueStateArgs(false, toggleActive: true, applyActive: CanWrite));
                    break;
                case ValueState.Number:
                    SetValueState(cell, new ValueStateArgs(false, typeLabelActive: true, inputActive: true, applyActive: CanWrite));
                    break;
                case ValueState.String:
                    if (LastValueWasNull)
                        SetValueState(cell, new ValueStateArgs(true, subContentButtonActive: true));
                    else
                        SetValueState(cell, new ValueStateArgs(true, false, SignatureHighlighter.StringOrange, subContentButtonActive: true));
                    break;
                case ValueState.Enum:
                    SetValueState(cell, new ValueStateArgs(true, subContentButtonActive: CanWrite));
                    break;
                case ValueState.Color:
                case ValueState.ValueStruct:
                    if (ParseUtility.CanParse(LastValueType))
                        SetValueState(cell, new ValueStateArgs(false, false, null, true, false, true, CanWrite, true, true));
                    else
                        SetValueState(cell, new ValueStateArgs(true, inspectActive: true, subContentButtonActive: true));
                    break;
                case ValueState.Collection:
                case ValueState.Dictionary:
                    SetValueState(cell, new ValueStateArgs(true, inspectActive: !LastValueWasNull, subContentButtonActive: !LastValueWasNull));
                    break;
                case ValueState.Unsupported:
                    SetValueState(cell, new ValueStateArgs(true, inspectActive: !LastValueWasNull));
                    break;
            }

            cell.RefreshSubcontentButton();
        }

        protected virtual void SetValueState(CacheObjectCell cell, ValueStateArgs args)
        {
            // main value label
            if (args.valueActive)
            {
                cell.ValueLabel.text = ValueLabelText;
                cell.ValueLabel.supportRichText = args.valueRichText;
                cell.ValueLabel.color = args.valueColor;
            }
            else
                cell.ValueLabel.text = "";

            // Type label (for primitives)
            cell.TypeLabel.gameObject.SetActive(args.typeLabelActive);
            if (args.typeLabelActive)
                cell.TypeLabel.text = SignatureHighlighter.Parse(LastValueType, false);

            // toggle for bools
            cell.Toggle.gameObject.SetActive(args.toggleActive);
            if (args.toggleActive)
            {
                cell.Toggle.interactable = CanWrite;
                cell.Toggle.isOn = (bool)Value;
                cell.ToggleText.text = Value.ToString();
            }

            // inputfield for numbers
            cell.InputField.UIRoot.SetActive(args.inputActive);
            if (args.inputActive)
            {
                cell.InputField.Text = ParseUtility.ToStringForInput(Value, LastValueType);
                cell.InputField.Component.readOnly = !CanWrite;
            }

            // apply for bool and numbers
            cell.ApplyButton.Component.gameObject.SetActive(args.applyActive);

            // Inspect button only if last value not null.
            if (cell.InspectButton != null)
                cell.InspectButton.Component.gameObject.SetActive(args.inspectActive && !LastValueWasNull);

            // allow IValue for null strings though
            cell.SubContentButton.Component.gameObject.SetActive(args.subContentButtonActive && (!LastValueWasNull || State == ValueState.String));
        }

        // CacheObjectCell Apply

        public virtual void OnCellApplyClicked()
        {
            if (State == ValueState.Boolean)
                SetUserValue(this.CellView.Toggle.isOn);
            else
            {
                if (ParseUtility.TryParse(CellView.InputField.Text, LastValueType, out object value, out Exception ex))
                {
                    SetUserValue(value);
                }
                else
                {
                    ExplorerCore.LogWarning("Unable to parse input!");
                    if (ex != null)
                        ExplorerCore.Log(ex.ReflectionExToString());
                }
            }

            SetDataToCell(this.CellView);
        }

        // IValues

        public virtual void OnCellSubContentToggle()
        {
            if (this.IValue == null)
            {
                var ivalueType = InteractiveValue.GetIValueTypeForState(State);

                if (ivalueType == null)
                    return;

                IValue = (InteractiveValue)Pool.Borrow(ivalueType);
                CurrentIValueType = ivalueType;

                IValue.OnBorrowed(this);
                IValue.SetValue(this.Value);
                IValue.UIRoot.transform.SetParent(CellView.SubContentHolder.transform, false);
                CellView.SubContentHolder.SetActive(true);
                SubContentShowWanted = true;

                // update our cell after creating the ivalue (the value may have updated, make sure its consistent)
                this.ProcessOnEvaluate();
                this.SetDataToCell(this.CellView);
            }
            else
            {
                SubContentShowWanted = !SubContentShowWanted;
                CellView.SubContentHolder.SetActive(SubContentShowWanted);

                if (SubContentShowWanted && IValue.PendingValueWanted)
                {
                    IValue.PendingValueWanted = false;
                    this.ProcessOnEvaluate();
                    this.SetDataToCell(this.CellView);
                    IValue.SetValue(this.Value);
                }
            }

            CellView.RefreshSubcontentButton();
        }

        public virtual void ReleaseIValue()
        {
            if (IValue == null)
                return;

            IValue.ReleaseFromOwner();
            Pool.Return(CurrentIValueType, IValue);

            IValue = null;
        }

        internal static GameObject InactiveIValueHolder
        {
            get
            {
                if (!inactiveIValueHolder)
                {
                    inactiveIValueHolder = new GameObject("Temp_IValue_Holder");
                    GameObject.DontDestroyOnLoad(inactiveIValueHolder);
                    inactiveIValueHolder.transform.parent = UIManager.PoolHolder.transform;
                    inactiveIValueHolder.SetActive(false);
                }
                return inactiveIValueHolder;
            }
        }
        private static GameObject inactiveIValueHolder;

        // Value state args helper

        public struct ValueStateArgs
        {
            public ValueStateArgs(bool valueActive = true, bool valueRichText = true, Color? valueColor = null,
                bool typeLabelActive = false, bool toggleActive = false, bool inputActive = false, bool applyActive = false,
                bool inspectActive = false, bool subContentButtonActive = false)
            {
                this.valueActive = valueActive;
                this.valueRichText = valueRichText;
                this.valueColor = valueColor == null ? Color.white : (Color)valueColor;
                this.typeLabelActive = typeLabelActive;
                this.toggleActive = toggleActive;
                this.inputActive = inputActive;
                this.applyActive = applyActive;
                this.inspectActive = inspectActive;
                this.subContentButtonActive = subContentButtonActive;
            }

            public static ValueStateArgs Default => _default;
            private static ValueStateArgs _default = new ValueStateArgs(true);

            public bool valueActive, valueRichText, typeLabelActive, toggleActive,
                inputActive, applyActive, inspectActive, subContentButtonActive;

            public Color valueColor;
        }
    }
}
