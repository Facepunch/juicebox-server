import React from "react";
import FieldHeader from "./FieldHeader";

import { FieldContainer } from "./Form.styles";
interface Props {
    placeholder?: string;
    rows?: number;
    value?: string;
    label?: string;
    maxLength?: number;
    onChange?: (newValue: string) => void;
    disabled?: boolean;
}

const Textarea: React.FC<Props> = ({
    placeholder,
    value,
    rows,
    label,
    maxLength,
    onChange,
    disabled
}) => {
    return (
        <FieldContainer>
            <FieldHeader label={label} maxLength={maxLength} />
            <div className="field-body">
                <textarea
                    className="input textarea"
                    placeholder={placeholder}
                    value={value}
                    rows={rows}
                    onChange={(e) => onChange?.(e.target.value)}
                    disabled={!!disabled}
                ></textarea>
            </div>
        </FieldContainer>
    );
};

export default Textarea;
