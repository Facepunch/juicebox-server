import React, { useState, useEffect } from "react";
import { useFormModel } from "../../models/FormModel";
import { JuiceboxInput } from "../../models/JuiceboxInterfaces";
import Input from "../Form/Input";

interface Props {
    readonly control: JuiceboxInput;
    readonly disabled: boolean;
}

const InputControl: React.FC<Props> = ({ control, disabled }) => {
    const formModel = useFormModel();
    const [value, setValue] = useState(control?.Value ?? "");
    useEffect(
        () => formModel.addField(control.Key, value),
        [formModel, control, value]
    );

    return (
        <Input
            label={control.Label}
            placeholder={control.Placeholder}
            maxLength={control.MaxLength}
            type={control.Type}
            value={value}
            onChange={setValue}
            disabled={disabled}
        />
    );
};

export default InputControl;
