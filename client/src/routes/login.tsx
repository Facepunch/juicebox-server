import React, { useState } from "react";
import Layout from "../components/Layout";
import styled from "styled-components";
import { VideogameAsset } from "@material-ui/icons";
import { useNavigate } from "react-router-dom";

import FormContainer from "../components/AnswerArea/FormContainer";
import Input from "../components/Form/Input";
import Button from "../components/Button";
import { useGameModel } from "../models/GameModel";

import { ErrorOutline } from "@material-ui/icons";

const BodyContainer = styled.div`
    align-items: center;
    justify-content: center;
    display: flex;
    flex-direction: column;
    flex-grow: 1;
    text-align: center;
    width: 100%;

    h1 {
        text-transform: uppercase;
        letter-spacing: 2px;
        font-size: 1.5rem;
        margin-bottom: 0.5rem;
    }

    p {
        color: ${(props) => props.theme.colors.lightPurple};
        font-size: 1rem;
        font-weight: 400;
        max-width: 70%;
    }
`;

const IconWrapper = styled.div`
    background-color: rgba(0, 0, 0, 0.2);
    border-radius: 40px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 48px;
    margin-bottom: 1rem;
    width: 80px;
    height: 80px;

    svg {
        color: ${(props) => props.theme.colors.lightPurple};
    }
`;

const ErrorMessage = styled.div`
    align-items: center;
    background-color: ${(props) => `${props.theme.colors.orange}20`};
    border: 2px solid ${(props) => `${props.theme.colors.orange}50`};
    border-radius: 8px;
    color: ${(props) => props.theme.colors.orange};
    display: flex;
    padding: 8px 12px;
    text-transform: uppercase;
    font-size: 0.75rem;
    letter-spacing: 1px;

    svg {
        margin-right: 8px;
    }
`;

const Login: React.FC = () => {
    const navigate = useNavigate();
    const gameModel = useGameModel();
    const [roomCode, setRoomCode] = useState("");
    const [username, setUsername] = useState("");
    const [error, setError] = useState("");

    async function loginHandler() {
        const result = await gameModel.join(roomCode, username);
        switch (result) {
            case "success":
                navigate("/play");
                break;
            default:
                setError(result);
                break;
        }
    }

    return (
        <Layout>
            <BodyContainer>
                <IconWrapper>
                    <VideogameAsset fontSize="inherit" />
                </IconWrapper>

                <h1>Connect to a Game</h1>
                <p>
                    Enter the code from a s&amp;box server to connect and play
                    in real-time.
                </p>
            </BodyContainer>
            <FormContainer>
                <Input
                    label="Room Code"
                    type="text"
                    placeholder="XXXXXX"
                    maxLength={8}
                    value={roomCode}
                    onChange={(s) => setRoomCode(s.toUpperCase())}
                />
                <Input
                    label="Your name"
                    type="text"
                    placeholder="Enter your name..."
                    maxLength={64}
                    value={username}
                    onChange={setUsername}
                />
                {!!error && (
                    <ErrorMessage>
                        <ErrorOutline fontSize="small" />
                        {error}
                    </ErrorMessage>
                )}
                <Button
                    label="Join"
                    onClick={loginHandler}
                    disabled={roomCode.length < 6 || username.trim().length === 0}
                />
            </FormContainer>
        </Layout>
    );
};

export default Login;
