import React, { useCallback, useState } from "react";
import styled, { keyframes } from "styled-components";
import { StyledButton } from "./Button.styles";

import { Refresh } from "@material-ui/icons";

const spinAnimation = keyframes`
    from {
        transform: rotate(0deg);
    }
    to {
        transform: rotate(360deg);
    }
`;

const IconContainer = styled.div`
    align-items: center;
    justify-content: center;
    display: flex;
    opacity: 0.7;

    svg {
        animation-name: ${spinAnimation};
        animation-duration: 2s;
        animation-iteration-count: infinite;
        margin-right: 4px;
    }
`;

interface Props {
    label?: string;
    variant?: string;
    onClick?: () => void | Promise<void>;
    disabled?: boolean;
}

const Button: React.FC<Props> = ({ label, variant, onClick, disabled }) => {
    const [loading, setLoading] = useState(false);
    const clickHandler = useCallback(async () => {
        try {
            setLoading(true);
            const result = onClick?.();
            if (result instanceof Promise) {
                await result;
            }
        } finally {
            setLoading(false);
        }
    }, [onClick, setLoading]);
    return (
        <StyledButton
            className={variant}
            onClick={loading ? null : clickHandler}
            disabled={!!disabled}
        >
            {loading && (
                <IconContainer>
                    <Refresh />
                    <span>Loading</span>
                </IconContainer>
            )}
            {!loading && label}
        </StyledButton>
    );
};

export default Button;
