import React from "react";
import FieldHeader from "./FieldHeader";
import { FieldContainer } from "./Form.styles";
interface Props {
    placeholder?: string;
    type?: string;
    value?: string;
    maxLength?: number;
    label?: string;
    onChange?: (newValue: string) => void;
    disabled?: boolean;
}

const Input: React.FC<Props> = ({
    placeholder,
    type,
    value,
    maxLength,
    label,
    onChange,
    disabled
}) => {
    return (
        <FieldContainer>
            <FieldHeader
                label={label}
                maxLength={
                    maxLength ? maxLength - (value?.length ?? 0) : undefined
                }
            />
            <div className="field-body">
                <input
                    className={`input ${value?.length !== 0 && "is-active"}`}
                    type={type}
                    placeholder={placeholder}
                    value={value}
                    maxLength={maxLength}
                    onChange={(e) => onChange?.(e.target.value)}
                    required
                    disabled={!!disabled}
                ></input>
            </div>
        </FieldContainer>
    );
};

export default Input;
