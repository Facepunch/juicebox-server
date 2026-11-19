import { createGlobalStyle } from "styled-components"
 
import background from "../images/background.jpg";


export default createGlobalStyle`
    *{
        margin: 0;
        padding: 0;
        outline:0;
        box-sizing:border-box;
    }
    #root{
        margin: 0 auto;
        display: flex;
        flex-direction: column;
        height: 100%;
    }
    body {
        background-color: #1F113E;
        background-image: url(${background});
        background-size: cover;
        background-position: center;
        color: #fff;
        font-size: 16px;
        font-family: 'Poppins', sans-serif;
        font-weight: 600;
        height: 100%;
    }
    html {
        height: 100%;
    }

    input, textarea {
        color: red;
    }

    .container {
        padding: 2rem;
        width: 100%;
    }
 `