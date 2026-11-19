import React, { useEffect, useState } from "react";
import { useFormModel } from "../../models/FormModel";
import { JuiceboxTextarea } from "../../models/JuiceboxInterfaces";
import Textarea from "../Form/Textarea";

interface Props {
    readonly control: JuiceboxTextarea;
    readonly disabled: boolean;
}

const TextareaControl: React.FC<Props> = ({ control, disabled }) => {
    const formModel = useFormModel();
    const [value, setValue] = useState(control?.Value ?? "");
    useEffect(
        () => formModel.addField(control.Key, value),
        [formModel, control, value]
    );

    return (
        <Textarea
            label={control.Label}
            placeholder={control.Placeholder}
            maxLength={control.MaxLength}
            rows={control.Rows}
            value={value}
            onChange={setValue}
            disabled={disabled}
        />
    );
};

export default TextareaControl;
