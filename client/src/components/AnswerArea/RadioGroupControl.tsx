import React, { useEffect, useState } from "react";
import styled from "styled-components";
import { useFormModel } from "../../models/FormModel";
import { JuiceboxRadioGroup } from "../../models/JuiceboxInterfaces";
import Radio from "../Form/Radio";

const RadioGroup = styled.div`
    flex-direction: column;
`;

interface Props {
    readonly control: JuiceboxRadioGroup;
    readonly disabled: boolean;
}

const RadioGroupControl: React.FC<Props> = ({ control, disabled }) => {
    const formModel = useFormModel();
    const [value, setValue] = useState("");
    useEffect(
        () => formModel.addField(control.Key, value),
        [formModel, control, value]
    );

    if (!Array.isArray(control?.Options) || control.Options.length === 0) {
        return null;
    }

    return (
        <RadioGroup>
            {control.Options.map((o, i) => (
                <Radio
                    key={`${i}-${o.Value}`}
                    label={o.Label}
                    checked={value === o.Value}
                    onChecked={() => setValue(o.Value)}
                    disabled={disabled}
                />
            ))}
        </RadioGroup>
    );
};

export default RadioGroupControl;
