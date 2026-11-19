import React from "react";
import styled from "styled-components";

const FieldLabel = styled.div`
    color: ${(props) => props.theme.colors.textLight};
    display: flex;
    font-size: 14px;
    letter-spacing: 2px;
    text-transform: uppercase;
    margin-bottom: 0.75rem;
`;

const FieldTitle = styled.label`
    flex-grow: 1;
    display: flex;
`;

const FieldValidation = styled.span`
    color: ${(props) => props.theme.colors.textLight};
    text-align: right;
    opacity: 0.5;
    transition: ${(props) => props.theme.transitionDefault};

    &.warning {
        color: red;
        opacity: 1;
    }
`;

interface Props {
    label?: string;
    maxLength?: number;
}

const FieldHeader: React.FC<Props> = ({ label, maxLength }) => {
    return (
        <FieldLabel>
            <FieldTitle>{label}</FieldTitle>
            {maxLength != null && (
                <FieldValidation className={`${!maxLength && "warning"}`}>
                    {maxLength}
                </FieldValidation>
            )}
        </FieldLabel>
    );
};

export default FieldHeader;
