import React from "react";
import { FieldContainer } from "./Form.styles";

import {
    RadioButtonCheckedRounded,
    RadioButtonUnchecked
} from "@material-ui/icons";

interface Props {
    label?: string;
    checked?: boolean;
    onChecked?: () => void;
    disabled?: boolean;
}

const Radio: React.FC<Props> = ({ label, checked, onChecked, disabled }) => {
    return (
        <FieldContainer>
            <div
                className={`radio ${checked ? "is-toggled" : ""}`}
                onClick={checked ? null : onChecked}
            >
                <label>{label}</label>
                <div className="radioIcon">
                    {checked ? (
                        <RadioButtonCheckedRounded fontSize="inherit" />
                    ) : (
                        <RadioButtonUnchecked fontSize="inherit" />
                    )}
                </div>
            </div>
        </FieldContainer>
    );
};

export default Radio;
