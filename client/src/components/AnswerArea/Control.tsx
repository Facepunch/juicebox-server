import React from "react";
import { JuiceboxFormControl } from "../../models/JuiceboxInterfaces";
import ButtonControl from "./ButtonControl";
import DrawingControl from "./DrawingControl";
import InputControl from "./InputControl";
import RadioGroupControl from "./RadioGroupControl";
import TextareaControl from "./TextareaControl";

interface Props {
    readonly control?: JuiceboxFormControl;
    readonly disabled?: boolean;
}

const Control: React.FC<Props> = ({ control, disabled }) => {
    switch (control?.Type) {
        case "Input":
            return <InputControl control={control} disabled={disabled} />;
        case "Textarea":
            return <TextareaControl control={control} disabled={disabled} />;
        case "RadioGroup":
            return <RadioGroupControl control={control} disabled={disabled} />;
        case "Drawing":
            return <DrawingControl control={control} disabled={disabled} />;
        case "Button":
            return <ButtonControl control={control} disabled={disabled} />;
        default:
            return null;
    }
};

export default Control;
