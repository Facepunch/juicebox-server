import React from "react";
import styled from "styled-components";
import { JuiceboxStage } from "../../models/JuiceboxInterfaces";

const StageContainer = styled.div`
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 2rem 0;
    flex-grow: 1;
`;

const StyledText = styled.span`
    color: #fff;
    background: linear-gradient(to top, #fcecff, #fff);
    -webkit-text-fill-color: transparent;
    -webkit-background-clip: text;
    font-size: 3rem;
    padding: 0 6px;
    text-transform: uppercase;
    text-align: center;
    line-height: 2.5rem;
`;

interface Props {
    readonly stage?: JuiceboxStage;
}

const Stage: React.FC<Props> = ({ stage }) => {
    return (
        <StageContainer>
            <StyledText>{stage?.Title ?? ""}</StyledText>
            <StyledText>{stage?.Subtitle ?? ""}</StyledText>
        </StageContainer>
    );
};

export default Stage;
