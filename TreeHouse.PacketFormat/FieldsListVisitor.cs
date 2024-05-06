using TreeHouse.Common;

namespace TreeHouse.PacketFormat;

public abstract class FieldsListVisitor<TParam>
{
    public virtual void VisitFieldsList(FieldsList fieldsList, TParam param)
    {
        foreach ((IFieldItem item, int index) in fieldsList.Fields.WithIndex())
        {
            VisitFieldItem(item, index, param);
        }
    }

    protected virtual void VisitFieldItem(IFieldItem item, int index, TParam param)
    {
        switch (item)
        {
            case Field field: VisitField(field, index, param); break;
            case Branch branch: VisitBranch(branch.Details, index, param); break;
            default: break;
        }
    }

    protected virtual void VisitField(Field field, int index, TParam param)
    {
        switch (field.Type)
        {
            case PrimitiveFieldType primitive: VisitPrimitive(field, index, primitive, param); break;
            case LimitedStringFieldType limitedString: VisitLimitedString(field, index, limitedString, param); break;
            case ArrayFieldType array: VisitArray(field, index, array, param); break;
            case EnumFieldType enumType: VisitEnum(field, index, enumType, param); break;
            default: break;
        }
    }

    protected virtual void VisitPrimitive(Field field, int index, PrimitiveFieldType type, TParam param)
    {
        if (type.Value.StartsWith(':'))
            VisitStruct(field, index, type.Value[1..], param);
        else
            VisitIntrinsic(field, index, type.Value, param);
    }

    protected virtual void VisitStruct(Field field, int index, string type, TParam param)
    {
    }

    protected virtual void VisitIntrinsic(Field field, int index, string type, TParam param)
    {
    }

    protected virtual void VisitLimitedString(Field field, int index, LimitedStringFieldType type, TParam param)
    {
    }

    protected virtual void VisitArray(Field field, int index, ArrayFieldType type, TParam param)
    {
    }

    protected virtual void VisitEnum(Field field, int index, EnumFieldType type, TParam param)
    {
    }

    protected virtual void VisitBranch(BranchDetails branch, int index, TParam param)
    {
        if (branch.IsTrue != null)
            VisitFieldsList(branch.IsTrue, param);

        if (branch.IsFalse != null)
            VisitFieldsList(branch.IsFalse, param);
    }
}
