import React from "react";
import styled from "styled-components";

const AnswerAreaContainer = styled.div`
    max-width: 600px;
    width: 100%;
`;

const AnswerHeader = styled.div`
    background-color: ${(props) => `${props.theme.colors.lightPurple}50`};
    padding: 10px;
    border-radius: 8px 8px 0 0;
    padding: 0.75rem;
    text-align: center;
    text-transform: uppercase;
    text-shadow: 0 1px 0 rgba(0, 0, 0, 0.2);
    letter-spacing: 2px;
`;

const AnswerAreaBit = styled.div<{ headerLabel?: string }>`
    background-color: rgba(0, 0, 0, 0.7);
    border-radius: ${(props) => props.theme.borderRadius};
    border-radius: ${(props) =>
        props.headerLabel ? "0 0 8px 8px" : props.theme.borderRadius};
    box-shadow: 0 10px 20px rgba(0, 0, 0, 0.2);
    display: flex;
    flex-direction: column;
    padding: 2rem;
`;
interface Props {
    children?: React.ReactNode;
    headerLabel?: string;
}

const FormContainer: React.FC<Props> = ({ children, headerLabel }) => {
    return (
        <AnswerAreaContainer>
            {headerLabel && <AnswerHeader>{headerLabel}</AnswerHeader>}
            <AnswerAreaBit headerLabel={headerLabel}>{children}</AnswerAreaBit>
        </AnswerAreaContainer>
    );
};

export default FormContainer;
