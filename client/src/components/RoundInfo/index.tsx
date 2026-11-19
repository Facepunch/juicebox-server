import React, { useEffect, useReducer } from "react";
import styled from "styled-components";
import { useGameModel } from "../../models/GameModel";

const RoundInfoContainer = styled.div`
    box-shadow: 0 5px 10px rgba(0, 0, 0, 0.2);
    border-radius: 8px;
    display: flex;
    align-items: center;
    justify-content: center;
`;

const RoundInfoBox = styled.div`
    display: flex;

    flex-direction: row;
    align-items: center;
    justify-content: center;
    height: 40px;
    padding: 0 10px;
`;

const RoundContainer = styled(RoundInfoBox)`
    background-color: ${(props) => props.theme.colors.lightPurple};
    border-radius: 8px 0 0 8px;
    min-width: 40px;
`;
const TimeContainer = styled(RoundInfoBox)`
    background-color: ${(props) => `${props.theme.colors.lightPurple}50`};
    border-radius: 0 8px 8px 0;
    height: 40px;
    min-width: 80px;
`;
const StyledText = styled.span`
    color: #fff;
    font-size: 1rem;
    text-shadow: 0 1px 0 rgba(0, 0, 0, 0.1);
    text-transform: uppercase;
`;

const TimeCountdown: React.FC<{ readonly seconds: number }> = ({ seconds }) => {
    const [time, dispatch] = useReducer(
        (s: number, a: { type: "set" | "decrement"; value: number }) =>
            a.type === "set" ? a.value : Math.max(s - a.value, 0),
        seconds
    );
    useEffect(() => {
        dispatch({ type: "set", value: seconds });
        const interval = setInterval(() => {
            dispatch({ type: "decrement", value: 1 });
        }, 1000);
        return () => clearInterval(interval);
    }, [seconds]);

    const minutesValue = Math.floor(time / 60);
    const secondsValue = (time - minutesValue * 60).toString().padStart(2, "0");

    return <StyledText>{`${minutesValue}:${secondsValue}`}</StyledText>;
};

const RoundInfo: React.FC = () => {
    const { isPlaying, display } = useGameModel();

    if (!isPlaying || !display?.Header) {
        return null;
    } else {
        return (
            <RoundInfoContainer>
                {typeof display.Header.RoundNumber === "number" && (
                    <RoundContainer>
                        <StyledText>{display.Header.RoundNumber}</StyledText>
                    </RoundContainer>
                )}
                {typeof display.Header.RoundTime === "number" &&
                    display.Header.RoundTime > 0 && (
                        <TimeContainer>
                            <TimeCountdown
                                key={`${display.Header.RoundNumber}-${display.Header.RoundTime}`}
                                seconds={display.Header.RoundTime}
                            />
                        </TimeContainer>
                    )}
            </RoundInfoContainer>
        );
    }
};

export default RoundInfo;
