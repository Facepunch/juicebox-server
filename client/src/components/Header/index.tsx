import React from "react";
import styled from "styled-components";
import { useGameModel } from "../../models/GameModel";

import sboxLogo from "../../images/sbox-white.png";
import juiceboxLogo from "../../images/juicebox-orange.png";

import RoundInfo from "../RoundInfo";

import { Close } from "@material-ui/icons";

const StyledHeader = styled.header`
    border-bottom: 2px solid ${(props) => `${props.theme.colors.lightPurple}40`};
    display: flex;
    align-items: center;
    padding: 2rem;
`;

const BrandingLeft = styled.div<{ isPlaying: boolean }>`
    display: flex;
    align-items: center;
    flex-direction: row;
    justify-content: ${(props) => (!props.isPlaying ? "center" : "flex-start")};
    flex-grow: 1;
    height: 48px;
`;

const BrandingContainer = styled.img`
    position: relative;
    transition: ${(props) => props.theme.transitionDefault};
    top: 0;
    max-height: 48px;

    &:hover {
        top: -5px;
    }
`;

const SandboxLogo = styled(BrandingContainer)`
    max-height: 32px;
`;

const JuiceboxLogo = styled(BrandingContainer)``;

const CloseIcon = styled(Close)`
    font-size: 1rem;
    color: ${(props) => props.theme.colors.lightPurple};
    margin: 0 1rem;
`;

const Header: React.FC = () => {
    const { isPlaying } = useGameModel();

    return (
        <StyledHeader>
            <BrandingLeft isPlaying={isPlaying}>
                <SandboxLogo src={sboxLogo} />
                <CloseIcon />
                <JuiceboxLogo src={juiceboxLogo} />
            </BrandingLeft>
            <RoundInfo />
        </StyledHeader>
    );
};

export default Header;
