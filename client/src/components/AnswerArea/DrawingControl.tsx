import { Check, Clear, Undo } from "@material-ui/icons";
import React, { useEffect, useRef, useState } from "react";
import { ReactSketchCanvas, ReactSketchCanvasRef } from "react-sketch-canvas";
import styled from "styled-components";
import { useFormModel } from "../../models/FormModel";
import { JuiceboxDrawing } from "../../models/JuiceboxInterfaces";

const DrawButtons = styled.div`
    margin-bottom: 8px;
    display: flex;
    flex-direction: row;
`;

const DrawFooter = styled.div`
    margin-top: 8px;
    display: flex;
    flex-direction: row;
`;

const DrawBtn = styled.button`
    border: none;
    border-radius: 4px;
    padding: 0;
    width: 44px;
    height: 44px;
    margin: 0 1px;
`;

const Sizes = styled.div`
    align-items: center;
    justify-content: flex-end;
    display: flex;
    flex-direction: row;
    flex-grow: 1;
`;

const Colors = styled.div`
    align-items: center;
    justify-content: center;
    display: flex;
    flex-direction: row;
    flex-grow: 1;
`;

const ColorBtn = styled.button`
    border: 2px solid rgba(255, 255, 255, 0.3);

    border-radius: 4px;
    width: 40px;
    height: 40px;
    margin: 0 1px;

    &.is-active {
        border-color: #fff;
    }
`;

interface Props {
    readonly control: JuiceboxDrawing;
    readonly disabled: boolean;
}

const DrawingControl: React.FC<Props> = ({ control, disabled }) => {
    const formModel = useFormModel();
    const canvasRef = useRef<ReactSketchCanvasRef>(null);
    const [brushColor, setBrushColor] = useState("black");
    const [brushSize, setBrushSize] = useState(6);

    useEffect(
        () =>
            formModel.addField(control.Key, async () =>
                canvasRef.current
                    ? await canvasRef.current.exportImage("png")
                    : ""
            ),
        [formModel, control]
    );

    return (
        <>
            <DrawButtons>
                <DrawBtn
                    type="button"
                    style={{ backgroundColor: "#0A71F1", color: "white" }}
                    onClick={() => canvasRef.current?.undo()}
                >
                    <Undo />
                </DrawBtn>
                <DrawBtn
                    type="button"
                    style={{ backgroundColor: "#0A71F1", color: "white" }}
                    onClick={() => canvasRef.current?.clearCanvas()}
                >
                    <Clear />
                </DrawBtn>
                <Sizes>
                    <DrawBtn
                        type="button"
                        style={{
                            backgroundColor: "#0A71F1",
                            color: "white"
                        }}
                        onClick={() => setBrushSize(2)}
                    >
                        <span>S</span>
                    </DrawBtn>
                    <DrawBtn
                        type="button"
                        style={{
                            backgroundColor: "#0A71F1",
                            color: "white"
                        }}
                        onClick={() => setBrushSize(6)}
                    >
                        <span>M</span>
                    </DrawBtn>
                    <DrawBtn
                        type="button"
                        style={{
                            backgroundColor: "#0A71F1",
                            color: "white"
                        }}
                        onClick={() => setBrushSize(14)}
                    >
                        <span>L</span>
                    </DrawBtn>
                    <DrawBtn
                        type="button"
                        style={{
                            backgroundColor: "#0A71F1",
                            color: "white"
                        }}
                        onClick={() => setBrushSize(20)}
                    >
                        <span>XL</span>
                    </DrawBtn>
                </Sizes>
            </DrawButtons>

            <ReactSketchCanvas
                ref={canvasRef}
                strokeWidth={brushSize}
                strokeColor={brushColor}
                style={{
                    width: "100%",
                    aspectRatio: 600 / 400,
                    position: "relative",
                    userSelect: "none",
                    pointerEvents: disabled ? "none" : "all"
                }}
            />

            <DrawFooter>
                <Colors>
                    <ColorBtn
                        type="button"
                        style={{ backgroundColor: "white", color: "black" }}
                        onClick={() => setBrushColor("white")}
                    >
                        {brushColor === "white" && <Check />}
                    </ColorBtn>
                    <ColorBtn
                        type="button"
                        style={{ backgroundColor: "black", color: "white" }}
                        onClick={() => setBrushColor("black")}
                    >
                        {brushColor === "black" && <Check />}
                    </ColorBtn>
                    <ColorBtn
                        type="button"
                        style={{ backgroundColor: "red", color: "white" }}
                        onClick={() => setBrushColor("red")}
                    >
                        {brushColor === "red" && <Check />}
                    </ColorBtn>
                    <ColorBtn
                        type="button"
                        style={{ backgroundColor: "green", color: "white" }}
                        onClick={() => setBrushColor("green")}
                    >
                        {brushColor === "green" && <Check />}
                    </ColorBtn>
                    <ColorBtn
                        type="button"
                        style={{ backgroundColor: "blue", color: "white" }}
                        onClick={() => setBrushColor("blue")}
                    >
                        {brushColor === "blue" && <Check />}
                    </ColorBtn>
                    <ColorBtn
                        type="button"
                        style={{
                            backgroundColor: "yellow",
                            color: "black"
                        }}
                        onClick={() => setBrushColor("yellow")}
                    >
                        {brushColor === "yellow" && <Check />}
                    </ColorBtn>
                </Colors>
            </DrawFooter>
        </>
    );
};

export default DrawingControl;
