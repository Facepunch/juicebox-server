import React, { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { useGameModel } from "../models/GameModel";

import Layout from "../components/Layout";
import Stage from "../components/Stage";
import Form from "../components/AnswerArea";

const Play: React.FC = () => {
    const navigate = useNavigate();
    const gameModel = useGameModel();
    const { isPlaying, display, submittedResponse } = gameModel;

    useEffect(() => {
        if (!isPlaying) {
            navigate("/");
        }
    });

    return (
        <Layout>
            <Stage stage={display?.Stage} />
            {display?.Form?.Controls && display.Form.Controls.length > 0 && (
                <Form form={display?.Form} disabled={submittedResponse} />
            )}
        </Layout>
    );
};

export default Play;
